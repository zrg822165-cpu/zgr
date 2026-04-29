using A11yFlow.Core.Actions;

namespace A11yFlow.Protocol.Actions;

public sealed record ActRequest(
    string RequestId,
    ActionTarget Target,
    string Action,
    IReadOnlyDictionary<string, object?> Arguments,
    ExecutionPolicy? ExecutionPolicy,
    ExpectedOutcome? ExpectedOutcome,
    int? TimeoutMs);
