using Allyflow.Core.Refs;
using Allyflow.Core.Snapshots;

namespace Allyflow.Core.Abstractions;

public interface ISnapshotBuilder
{
    SnapshotResult BuildActiveWindowSnapshot();

    SnapshotResult BuildWindowSnapshot(WindowRef windowRef);
}
