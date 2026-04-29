namespace Allyflow.Core.Actions;

public sealed record ActionTiming(
    DateTimeOffset StartedAt,
    DateTimeOffset FinishedAt,
    long DurationMs);
