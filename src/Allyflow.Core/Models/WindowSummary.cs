using Allyflow.Core.Refs;

namespace Allyflow.Core.Models;

public sealed record WindowSummary(
    WindowRef Ref,
    string Title,
    int ProcessId,
    string? FrameworkId,
    bool IsActive);
