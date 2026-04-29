using Allyflow.Core.Models;
using Allyflow.Core.Refs;

namespace Allyflow.Core.Abstractions;

public interface IWindowRegistry
{
    IReadOnlyList<WindowSummary> ListWindows();

    WindowSummary? GetActiveWindow();

    nint? GetNativeHandle(WindowRef windowRef);
}
