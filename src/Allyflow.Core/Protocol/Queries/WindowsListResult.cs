using Allyflow.Core.Models;
using Allyflow.Core.Refs;

namespace Allyflow.Protocol.Queries;

public sealed record WindowsListResult(
    IReadOnlyList<WindowSummary> Windows,
    int Count,
    WindowRef? ActiveWindowRef,
    IReadOnlyDictionary<string, string?> Diagnostics);
