using Allyflow.Core.Actions;
using Allyflow.Core.Models;

namespace Allyflow.Core.Abstractions;

public interface ITargetResolver
{
    ToolResult<ResolvedTarget> Resolve(ActionTarget target);
}
