using OpenClaw.Core.Models;
using OpenClaw.Core.Refs;

namespace OpenClaw.Core.Snapshots;

public sealed record SnapshotResult(
    WindowRef WindowRef,
    string SnapshotVersion,
    ElementNode Root,
    string SummaryText,
    IReadOnlyDictionary<string, string?> Diagnostics);
