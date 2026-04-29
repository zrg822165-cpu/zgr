namespace A11yFlow.Core.Actions;

public sealed record ExpectedOutcome(
    string Type,
    IReadOnlyDictionary<string, object?> Conditions,
    int SettleTimeoutMs);
