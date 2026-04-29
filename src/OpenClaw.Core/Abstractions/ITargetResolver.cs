using OpenClaw.Core.Actions;
using OpenClaw.Core.Models;

namespace OpenClaw.Core.Abstractions;

public interface ITargetResolver
{
    ToolResult<ResolvedTarget> Resolve(ActionTarget target);
}
