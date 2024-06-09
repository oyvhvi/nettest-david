namespace atn062024.Models;

public record PlayerImpactReport(Guid PlayerId, String PlayerName, decimal Impact, Int32 NumberOfPlaythroughs, TimeSpan TotalTimePlayed);
