using Allyflow.Core.Models;
using Allyflow.Core.Refs;

namespace Allyflow.Core.Abstractions;

public interface ISnapshotFormatter
{
    string Format(WindowSummary window, ElementNode root, string snapshotVersion, ElementRef? focusedElementRef);
}
