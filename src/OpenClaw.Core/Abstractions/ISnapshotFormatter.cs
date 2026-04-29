using OpenClaw.Core.Models;
using OpenClaw.Core.Refs;

namespace OpenClaw.Core.Abstractions;

public interface ISnapshotFormatter
{
    string Format(WindowSummary window, ElementNode root, string snapshotVersion, ElementRef? focusedElementRef);
}
