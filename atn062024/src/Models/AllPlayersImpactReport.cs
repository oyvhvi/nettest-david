namespace atn062024.Models;

public record AllPlayersImpactReport(
    // String? ContinuationToken
    IReadOnlyList<PlayerImpactReport> ImpactReports);
