using Allyflow.Core.Models;
using Allyflow.Core.Refs;

namespace Allyflow.Core.Snapshots;

public sealed record SnapshotResult(
    WindowRef WindowRef,
    string SnapshotVersion,
    ElementNode Root,
    string SummaryText,
    IReadOnlyDictionary<string, string?> Diagnostics);
