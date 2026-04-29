using OpenClaw.Core.Actions;
using OpenClaw.Core.Models;

namespace OpenClaw.Core.Abstractions;

public interface IActionExecutor
{
    ToolResult<ActionExecutionResult> Execute(ActionRequest request, ResolvedTarget target);
}
