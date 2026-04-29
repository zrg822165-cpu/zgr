using A11yFlow.Core.Models;
using A11yFlow.Core.Refs;

namespace A11yFlow.Core.Abstractions;

public interface IWindowRegistry
{
    IReadOnlyList<WindowSummary> ListWindows();

    WindowSummary? GetActiveWindow();

    nint? GetNativeHandle(WindowRef windowRef);
}
