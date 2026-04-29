using Allyflow.Core.Models;
using Allyflow.Core.Snapshots;

namespace Allyflow.Core.Actions;

public sealed record ResolvedTarget(
    RefEntry Entry,
    SnapshotResult? Snapshot,
    TargetResolution Resolution);
