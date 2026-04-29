using OpenClaw.Core.Models;
using OpenClaw.Core.Refs;

namespace OpenClaw.Core.Abstractions;

public interface IWindowRegistry
{
    IReadOnlyList<WindowSummary> ListWindows();

    WindowSummary? GetActiveWindow();

    nint? GetNativeHandle(WindowRef windowRef);
}
