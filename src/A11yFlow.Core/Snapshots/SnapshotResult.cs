using A11yFlow.Core.Models;
using A11yFlow.Core.Refs;

namespace A11yFlow.Core.Snapshots;

public sealed record SnapshotResult(
    WindowRef WindowRef,
    string SnapshotVersion,
    ElementNode Root,
    string SummaryText,
    IReadOnlyDictionary<string, string?> Diagnostics);
