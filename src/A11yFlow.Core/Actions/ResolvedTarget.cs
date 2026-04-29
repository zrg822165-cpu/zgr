using A11yFlow.Core.Models;
using A11yFlow.Core.Snapshots;

namespace A11yFlow.Core.Actions;

public sealed record ResolvedTarget(
    RefEntry Entry,
    SnapshotResult? Snapshot,
    TargetResolution Resolution);
