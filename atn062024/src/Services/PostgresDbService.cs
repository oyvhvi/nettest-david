using atn062024.Models;
using Dapper;
using Npgsql;
using Polly.Retry;
using System.Data.Common;
using System.Runtime.CompilerServices;

namespace atn062024.Services;

public sealed class PostgresDbService : IDbService
{
    private readonly DbDataSource _dataSource_;
    private readonly AsyncRetryPolicy _retryPolicy_;

    public PostgresDbService(IDataSourceProvider dataSourceProvider)
    {
        _dataSource_ = dataSourceProvider.BuildDataSource();
        _retryPolicy_ = dataSourceProvider.GetRetryPolicy();
    }

    public async Task<DbResult<Player>> InsertPlayerAsync(Guid playerId, String playerName, CancellationToken ct)
    {
        if(String.IsNullOrWhiteSpace(playerName))
        {
            return DbResult<Player>.CreateFailure(FailureType.BadRequest);
        }

        try
        {
            await ExecuteAsync(
            """
            insert into players (id,name)
            values (@id, @name)
            ;
            """,
            parameters: new { id = playerId, name = playerName },
            ct);
        }
        catch(PostgresException e) when (e.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            return DbResult<Player>.CreateFailure(FailureType.Conflict);
        }


        return DbResult<Player>.CreateSuccess(new Player(playerId, playerName));
    }

    public async Task<DbResult> InsertScoreAsync(Guid playerId, DateTimeOffset playStart, TimeSpan timeSpent, Int32 score, Decimal percentCorrectAnswers, CancellationToken ct)
    {
        if(percentCorrectAnswers is < 0 or > 100)
        {
            return DbResult.CreateFailure(FailureType.BadRequest);
        }

        try
        {
            await ExecuteAsync(
                """
                insert into scores (player_id, play_start, time_spent, score, percent_correct_answers)
                values (@playerId, @playStart, @timeSpent, @score, @percentCorrectAnswers)
                """,
                parameters: new { playerId = playerId, playStart = playStart, timeSpent = timeSpent, score = score, percentCorrectAnswers = percentCorrectAnswers },
                ct);
        }
        catch(PostgresException e) when (e.SqlState == PostgresErrorCodes.ForeignKeyViolation)
        {
            return DbResult.CreateFailure(FailureType.NotFound);
        }

        return DbResult.CreateSuccess();
    }

    public async Task<DbResult<List<PlayerScore>>> GetTopScoringPlayersAsync(DateTimeOffset fromInclusive, DateTimeOffset toExclusive, Int32 limit, CancellationToken ct)
    {
        if(fromInclusive >= toExclusive)
        {
            return DbResult<List<PlayerScore>>.CreateFailure(FailureType.BadRequest);
        }

        List<PlayerScore> playerScores = await Query<PlayerScore>(
            """
            with playerScores as
            (
                select distinct on (player_id) player_id, score
                from scores
                where play_start >= @fromInclusive and play_start < @toExclusive
                order by player_id, score desc
            )

            select s.player_id, p.name as player_name, s.score
            from playerScores s
            join players p on s.player_id = p.id
            order by s.score desc, s.player_id
            limit @limit
            """,
            new { fromInclusive = fromInclusive.UtcDateTime, toExclusive = toExclusive.UtcDateTime, limit = limit },
            ct)
            .ToListAsync(ct);

        return DbResult<List<PlayerScore>>.CreateSuccess(playerScores);
    }

    public async Task<DbResult<List<PlayerActivity>>> GetMostActivePlayersAsync(DateTimeOffset fromInclusive, DateTimeOffset toExclusive, Int32 limit, CancellationToken ct)
    {
        if (fromInclusive >= toExclusive)
        {
            return DbResult<List<PlayerActivity>>.CreateFailure(FailureType.BadRequest);
        }

        List<PlayerActivity> playerActivities = await Query<PlayerActivity>(
            """
            with playerActivities as
            (
                select player_id, count(1) as playthroughs
                from scores
                where play_start >= @fromInclusive and play_start < @toExclusive
                group by player_id
            )

            select a.player_id, p.name as player_name, cast(a.playthroughs as int) as playthroughs
            from playerActivities a
            join players p on a.player_id = p.id
            order by a.playthroughs desc, a.player_id
            limit @limit
            """,
            new { fromInclusive = fromInclusive.UtcDateTime, toExclusive = toExclusive.UtcDateTime, limit = limit },
            ct)
            .ToListAsync(ct);

        return DbResult<List<PlayerActivity>>.CreateSuccess(playerActivities);
    }

    public async Task<DbResult<List<PlayerImpactReport>>> GetImpactReportsAsync(CancellationToken ct)
    {
        List<PlayerImpactReport> impactReports = await Query<PlayerImpactReport>(
            """
            with
            first_percentage as
            (
                select distinct on (player_id) player_id, percent_correct_answers
                from scores
                order by player_id, play_start asc
            ),
            best_percentage as
            (
                select distinct on (player_id) player_id, percent_correct_answers
                from scores
                order by player_id, percent_correct_answers desc
            ),
            play_stats as
            (
                select player_id, count(1) as number_of_playthroughs, sum(time_spent) as total_time_played
                from scores
                group by player_id
            )

            select first.player_id, p.name as player_name, (best.percent_correct_answers - first.percent_correct_answers) as impact, cast(stats.number_of_playthroughs as int) as number_of_playthroughs, stats.total_time_played
            from first_percentage first
            join best_percentage best on first.player_id = best.player_id
            join play_stats stats on first.player_id = stats.player_id
            join players p on first.player_id = p.id
            order by first.player_id
            """,
            parameters: null,
            ct)
            .ToListAsync(ct);

        return DbResult<List<PlayerImpactReport>>.CreateSuccess(impactReports);
    }

    private async Task<Int32> ExecuteAsync(String commandText, Object? parameters, CancellationToken cancel) =>
      await _retryPolicy_.ExecuteAsync(async () =>
      {
          // connections are pooled so this isn't as bad as it may look
          await using DbConnection openedConnection = await _dataSource_.OpenConnectionAsync(cancel);

          var commandDefinition = new CommandDefinition(
              commandText,
              parameters,
              commandType: System.Data.CommandType.Text,
              flags: CommandFlags.Buffered,
              cancellationToken: cancel);

          return await openedConnection.ExecuteAsync(commandDefinition);
      });

    private async IAsyncEnumerable<T> Query<T>(String commandText, Object? parameters, [EnumeratorCancellation] CancellationToken ct) where T : notnull
    {
        // Could also consider
        // * caching if acceptable. e.g. redis cache that invalidates on writes
        // * Sharding is another possibility but may require aggregating final results from sharded results
        // * Precomputation jobs on certain reports

        // Connections are pooled so this isn't as bad as it may look here.
        await using DbConnection conn = await _dataSource_.OpenConnectionAsync(ct);

        IAsyncEnumerable<T> query = conn.QueryUnbufferedAsync<T>(
            commandText,
            parameters,
            commandType: System.Data.CommandType.Text);

        await foreach (T t in query.WithCancellation(ct))
        {
            yield return t;
        }
    }

    void IDisposable.Dispose()
    {
        _dataSource_.Dispose();
    }
}