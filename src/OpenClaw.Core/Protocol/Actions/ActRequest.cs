using OpenClaw.Core.Actions;

namespace OpenClaw.Protocol.Actions;

public sealed record ActRequest(
    string RequestId,
    ActionTarget Target,
    string Action,
    IReadOnlyDictionary<string, object?> Arguments,
    ExecutionPolicy? ExecutionPolicy,
    ExpectedOutcome? ExpectedOutcome,
    int? TimeoutMs);
