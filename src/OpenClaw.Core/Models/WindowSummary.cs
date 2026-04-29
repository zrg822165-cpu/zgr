using OpenClaw.Core.Refs;

namespace OpenClaw.Core.Models;

public sealed record WindowSummary(
    WindowRef Ref,
    string Title,
    int ProcessId,
    string? FrameworkId,
    bool IsActive);
