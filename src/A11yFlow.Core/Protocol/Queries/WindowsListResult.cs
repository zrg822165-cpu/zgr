using A11yFlow.Core.Models;
using A11yFlow.Core.Refs;

namespace A11yFlow.Protocol.Queries;

public sealed record WindowsListResult(
    IReadOnlyList<WindowSummary> Windows,
    int Count,
    WindowRef? ActiveWindowRef,
    IReadOnlyDictionary<string, string?> Diagnostics);
