using OpenClaw.Core.Models;
using OpenClaw.Core.Snapshots;

namespace OpenClaw.Core.Actions;

public sealed record ResolvedTarget(
    RefEntry Entry,
    SnapshotResult? Snapshot,
    TargetResolution Resolution);
