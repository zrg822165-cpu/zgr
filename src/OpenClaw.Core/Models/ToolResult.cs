namespace OpenClaw.Core.Models;

public sealed record ToolResult<T>(T? Payload, ToolError? Error)
{
    public bool IsSuccess => Error is null;
}
