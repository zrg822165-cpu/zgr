using A11yFlow.Core.Errors;

namespace A11yFlow.Core.Models;

public sealed record ToolError(
    ToolErrorCode Code,
    string Message,
    bool Retryable,
    string? SuggestedNextStep,
    IReadOnlyDictionary<string, string?> Diagnostics);
