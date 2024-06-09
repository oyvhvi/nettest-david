namespace atn062024.Models;

public sealed record ResiliencySettings(Int32 MaxRetries, Int32 BackoffMs);
