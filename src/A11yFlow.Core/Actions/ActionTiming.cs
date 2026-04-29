namespace A11yFlow.Core.Actions;

public sealed record ActionTiming(
    DateTimeOffset StartedAt,
    DateTimeOffset FinishedAt,
    long DurationMs);
