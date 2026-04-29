namespace OpenClaw.Core.Actions;

public sealed record ActionTiming(
    DateTimeOffset StartedAt,
    DateTimeOffset FinishedAt,
    long DurationMs);
