using OpenClaw.Core.Refs;
using OpenClaw.Core.Snapshots;

namespace OpenClaw.Core.Abstractions;

public interface ISnapshotBuilder
{
    SnapshotResult BuildActiveWindowSnapshot();

    SnapshotResult BuildWindowSnapshot(WindowRef windowRef);
}
