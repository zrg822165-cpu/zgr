using A11yFlow.Core.Models;
using A11yFlow.Core.Refs;

namespace A11yFlow.Core.Abstractions;

public interface ISnapshotFormatter
{
    string Format(WindowSummary window, ElementNode root, string snapshotVersion, ElementRef? focusedElementRef);
}
