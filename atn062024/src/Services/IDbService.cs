using atn062024.Models;

namespace atn062024.Services;

public interface IDbService : IDisposable
{
    Task<DbResult<Player>> InsertPlayerAsync(Guid playerId, String playerName, CancellationToken ct);

    Task<DbResult> InsertScoreAsync(Guid playerId, DateTimeOffset playStart, TimeSpan timeSpent, Int32 score, Decimal percentCorrectAnswers, CancellationToken ct);

    Task<DbResult<List<PlayerScore>>> GetTopScoringPlayersAsync(DateTimeOffset fromInclusive, DateTimeOffset toExclusive, Int32 limit, CancellationToken ct);

    Task<DbResult<List<PlayerActivity>>> GetMostActivePlayersAsync(DateTimeOffset fromInclusive, DateTimeOffset toExclusive, Int32 limit, CancellationToken ct);

    Task<DbResult<List<PlayerImpactReport>>> GetImpactReportsAsync(CancellationToken ct);
}
