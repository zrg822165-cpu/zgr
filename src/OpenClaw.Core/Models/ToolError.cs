using OpenClaw.Core.Errors;

namespace OpenClaw.Core.Models;

public sealed record ToolError(
    ToolErrorCode Code,
    string Message,
    bool Retryable,
    string? SuggestedNextStep,
    IReadOnlyDictionary<string, string?> Diagnostics);
