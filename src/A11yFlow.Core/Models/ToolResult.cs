namespace A11yFlow.Core.Models;

public sealed record ToolResult<T>(T? Payload, ToolError? Error)
{
    public bool IsSuccess => Error is null;
}
