using A11yFlow.Core.Actions;
using A11yFlow.Core.Models;

namespace A11yFlow.Core.Abstractions;

public interface ITargetResolver
{
    ToolResult<ResolvedTarget> Resolve(ActionTarget target);
}
