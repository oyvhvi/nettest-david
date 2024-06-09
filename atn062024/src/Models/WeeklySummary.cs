namespace atn062024.Models;

public record WeeklySummary(Int32 Year, Int32 WeekNumber, IReadOnlyList<PlayerScore> TopScoringPlayers, IReadOnlyList<PlayerActivity> MostActivePlayers);