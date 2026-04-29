using A11yFlow.Core.Actions;
using A11yFlow.Core.Models;

namespace A11yFlow.Core.Abstractions;

public interface IActionExecutor
{
    ToolResult<ActionExecutionResult> Execute(ActionRequest request, ResolvedTarget target);
}
