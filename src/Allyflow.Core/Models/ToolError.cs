using Allyflow.Core.Errors;

namespace Allyflow.Core.Models;

public sealed record ToolError(
    ToolErrorCode Code,
    string Message,
    bool Retryable,
    string? SuggestedNextStep,
    IReadOnlyDictionary<string, string?> Diagnostics);
