namespace A11yFlow.Core.Actions;

public sealed record ActionExecutionResult(
    bool Success,
    string ExecutionPath,
    IReadOnlyDictionary<string, string?> ObservedEffect,
    IReadOnlyDictionary<string, string?> Diagnostics);
