using Allyflow.Core.Models;

namespace Allyflow.Core.Actions;

public sealed record ActionResult(
    string RequestId,
    bool Success,
    ActionStatus Status,
    TargetResolution? TargetResolution,
    string Action,
    IReadOnlyDictionary<string, object?> Arguments,
    string ExecutionPath,
    int Retries,
    IReadOnlyDictionary<string, string?> ObservedEffect,
    VerificationResult Verification,
    ToolError? Error,
    IReadOnlyDictionary<string, string?> Diagnostics,
    ActionTiming Timing);
