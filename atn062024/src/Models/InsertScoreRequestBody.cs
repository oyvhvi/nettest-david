namespace atn062024.Models;

public record InsertScoreRequestBody(DateTimeOffset PlayStart, Int32 TimeSpentSeconds, Int32 Score, Decimal PercentCorrectAnswers);
