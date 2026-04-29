using A11yFlow.Core.Refs;

namespace A11yFlow.Core.Models;

public sealed record WindowSummary(
    WindowRef Ref,
    string Title,
    int ProcessId,
    string? FrameworkId,
    bool IsActive);
