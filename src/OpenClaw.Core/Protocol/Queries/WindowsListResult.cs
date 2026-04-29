using OpenClaw.Core.Models;
using OpenClaw.Core.Refs;

namespace OpenClaw.Protocol.Queries;

public sealed record WindowsListResult(
    IReadOnlyList<WindowSummary> Windows,
    int Count,
    WindowRef? ActiveWindowRef,
    IReadOnlyDictionary<string, string?> Diagnostics);
