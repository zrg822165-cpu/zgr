namespace A11yFlow.Core.Actions;

public sealed record ActionRequest(
    string RequestId,
    ActionTarget Target,
    string Action,
    IReadOnlyDictionary<string, object?> Arguments,
    ExecutionPolicy ExecutionPolicy,
    ExpectedOutcome? ExpectedOutcome,
    int TimeoutMs);
