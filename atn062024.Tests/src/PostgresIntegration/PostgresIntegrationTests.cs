using atn062024.Models;
using atn062024.Services;
using atn062024.Tests.Helpers;
using FluentAssertions;
using Moq;
using System.Data.Common;
using System.Globalization;
using Testcontainers.PostgreSql;

namespace atn062024.Tests;

public class PostgresIntegrationTests
{
    private static async Task RunWithNewDb(Func<IDbService, CancellationToken, Task> test)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var ct = cts.Token;

        await using PostgreSqlContainer postgrescontainer = PostgreSqlContainerFactory.GetNew();
        await postgrescontainer.StartAsync(ct);

        String connectionString = postgrescontainer.GetConnectionString();
        var secretProviderMock = new Mock<ISecretProvider>(MockBehavior.Strict);
        secretProviderMock.Setup(p => p.GetSecret(It.Is<SecretKey>(k => k == SecretKey.pgsql_connstring))).Returns(connectionString);
        ISecretProvider testSecretProvider = secretProviderMock.Object;

        using IDataSourceProvider datasourceProvider = new PostgresDbDataSourceProvider(testSecretProvider, new ResiliencySettings(3, 300));
        await using DbDataSource dataSource = datasourceProvider.BuildDataSource();
        await SchemaHelper.CreateSchemaAsync(dataSource, ct);

        using IDbService dbService = new PostgresDbService(datasourceProvider);

        await test(dbService, ct);
    }

    [Fact]
    public async Task InsertScoreForNonExistingPlayer_Fails() =>
        await RunWithNewDb(async (dbService, ct) =>
        {
            // Insert score for nonexisting player
            DbResult insertNonExistingScoreResult = await dbService.InsertScoreAsync(Guid.NewGuid(), DateTimeOffset.UtcNow, TimeSpan.FromSeconds(1), 1, 1, ct);
            insertNonExistingScoreResult.Success.Should().BeFalse();
            insertNonExistingScoreResult.FailureType.Should().Be(FailureType.NotFound);
        });

    [Fact]
    public async Task InsertDuplicatePlayer_Fails() =>
        await RunWithNewDb(async (dbService, ct) =>
        {
            // Insert new player
            Guid player1Id = Guid.Parse("368ccb30-f8b6-437c-bf80-acccbb744efe");
            String player1Name = "Player 1";
            DbResult<Player> insertNewPlayerResult = await dbService.InsertPlayerAsync(player1Id, player1Name, ct);
            insertNewPlayerResult.Success.Should().BeTrue();
            insertNewPlayerResult.FailureType.Should().BeNull();
            insertNewPlayerResult.Value.Should().Be(new Player(player1Id, player1Name));

            // Insert the same player again
            DbResult<Player> insertDuplicatePlayerResult = await dbService.InsertPlayerAsync(player1Id, player1Name, ct);
            insertDuplicatePlayerResult.Success.Should().BeFalse();
            insertDuplicatePlayerResult.FailureType.Should().Be(FailureType.Conflict);
            insertDuplicatePlayerResult.Value.Should().BeNull();
        });

    private record InsertedScore(Guid playerId, DateTimeOffset playStart, TimeSpan timeSpent, int score, decimal percentCorrectAnswers);

    [Fact]
    public async Task Reports_AsExpected() =>
        await RunWithNewDb(async (dbService, ct) =>
        {
            // Insert 20 players
            List<Player> players = new();
            for (int p = 0; p < 20; p++)
            {
                Guid id = Guid.NewGuid();
                var player = new Player(id, p.ToString());
                players.Add(player);
                DbResult<Player> insertNewPlayerResult = await dbService.InsertPlayerAsync(player.Id, player.Name, ct);
                insertNewPlayerResult.Success.Should().BeTrue();
            }

            // Insert many scores, simultaneously
            List<InsertedScore> insertedScores = new();
            for (int p = 0; p < players.Count; p++)
            {
                for (int s = 0; s < 20; s++)
                {
                    // TODO don't use random in tests
                    var inserted = new InsertedScore(players[p].Id, new DateTimeOffset(2024, 3, 1, 0, 0, p+s, TimeSpan.Zero), TimeSpan.FromSeconds(Random.Shared.Next(1, 30)), Random.Shared.Next(0, 1000), Random.Shared.Next(0, 100));
                    insertedScores.Add(inserted);
                }
            }

            List<Task> insertTasks = insertedScores.Select(async s =>
            {
                await Task.Yield();
                DbResult insertScoreResult = await dbService.InsertScoreAsync(s.playerId, s.playStart, s.timeSpent, s.score, s.percentCorrectAnswers, ct);
                insertScoreResult.Success.Should().BeTrue();
            }).ToList();
            await Task.WhenAll(insertTasks);

            // Create impact reports manually
            List<PlayerImpactReport> expectedImpactReports = new();
            List<PlayerScore> scores = new();
            List<PlayerActivity> activities = new();
            foreach (var playerScores in insertedScores.GroupBy(s => s.playerId))
            {
                Player player = players.Single(p => p.Id == playerScores.Key);
                decimal firstPercentCorrect = playerScores.OrderBy(s => s.playStart).First().percentCorrectAnswers;
                decimal bestPercentCorrect = playerScores.Max(s => s.percentCorrectAnswers);
                Int32 bestScore = playerScores.Max(s => s.score);
                decimal impact = bestPercentCorrect - firstPercentCorrect;
                TimeSpan totalTimePlayed = TimeSpan.FromTicks(playerScores.Sum(p => p.timeSpent.Ticks));

                expectedImpactReports.Add(new PlayerImpactReport(playerScores.Key, player.Name, impact, playerScores.Count(), totalTimePlayed));
                scores.Add(new PlayerScore(player.Id, player.Name, bestScore));
                activities.Add(new PlayerActivity(player.Id, player.Name, playerScores.Count()));
            }

            DbResult<List<PlayerImpactReport>> impactReports = await dbService.GetImpactReportsAsync(ct);
            impactReports.Success.Should().BeTrue();
            impactReports.Value.Should().NotBeNull();
            impactReports.Value!.Should().BeEquivalentTo(expectedImpactReports);

            // inserted values on 2024 march 1 which was the 9th week.
            var expectedWeeklySummary = new WeeklySummary(
                2024,
                9,
                TopScoringPlayers: scores.OrderByDescending(s => s.Score).ThenBy(p => p.PlayerId).Take(10).ToList(),
                MostActivePlayers: activities.OrderByDescending(a => a.Playthroughs).ThenBy(a => a.PlayerId).Take(10).ToList());

            // check weeks before and after are empty
            DbResult<List<PlayerScore>> scoresResultWeek8 = await dbService.GetTopScoringPlayersAsync(ISOWeek.ToDateTime(2024, 8, DayOfWeek.Monday), ISOWeek.ToDateTime(2024, 9, DayOfWeek.Monday), 10, ct);
            DbResult<List<PlayerScore>> scoresResultWeek10 = await dbService.GetTopScoringPlayersAsync(ISOWeek.ToDateTime(2024, 10, DayOfWeek.Monday), ISOWeek.ToDateTime(2024, 11, DayOfWeek.Monday), 10, ct);
            scoresResultWeek8.Success.Should().BeTrue();
            scoresResultWeek8.Value!.Count.Should().Be(0);
            scoresResultWeek10.Success.Should().BeTrue();
            scoresResultWeek10.Value!.Count.Should().Be(0);

            // check week 9
            DbResult<List<PlayerScore>> scoresResultWeek9 = await dbService.GetTopScoringPlayersAsync(ISOWeek.ToDateTime(2024, 9, DayOfWeek.Monday), ISOWeek.ToDateTime(2024, 10, DayOfWeek.Monday), 10, ct);
            scoresResultWeek9.Success.Should().BeTrue();
            scoresResultWeek9.Value!.Should()
                .ContainInConsecutiveOrder(expectedWeeklySummary.TopScoringPlayers)
                .And.HaveSameCount(expectedWeeklySummary.TopScoringPlayers);

            DbResult<List<PlayerActivity>> activityResultWeek9 = await dbService.GetMostActivePlayersAsync(ISOWeek.ToDateTime(2024, 9, DayOfWeek.Monday), ISOWeek.ToDateTime(2024, 10, DayOfWeek.Monday), 10, ct);
            activityResultWeek9.Success.Should().BeTrue();
            activityResultWeek9.Value!.Should()
                .ContainInConsecutiveOrder(expectedWeeklySummary.MostActivePlayers)
                .And.HaveSameCount(expectedWeeklySummary.MostActivePlayers);
        });
}