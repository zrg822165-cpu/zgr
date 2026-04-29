using A11yFlow.Core.Refs;
using A11yFlow.Core.Snapshots;

namespace A11yFlow.Core.Abstractions;

public interface ISnapshotBuilder
{
    SnapshotResult BuildActiveWindowSnapshot();

    SnapshotResult BuildWindowSnapshot(WindowRef windowRef);
}
