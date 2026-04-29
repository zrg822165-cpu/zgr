using Allyflow.Core.Actions;
using Allyflow.Core.Models;

namespace Allyflow.Core.Abstractions;

public interface IActionExecutor
{
    ToolResult<ActionExecutionResult> Execute(ActionRequest request, ResolvedTarget target);
}
