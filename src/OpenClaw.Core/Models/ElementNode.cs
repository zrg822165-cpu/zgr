using OpenClaw.Core.Refs;

namespace OpenClaw.Core.Models;

public sealed record ElementNode(
    ElementRef Ref,
    string Role,
    string? Name,
    string? AutomationId,
    string? ClassName,
    ElementBounds? Bounds,
    IReadOnlyList<string> States,
    IReadOnlyList<string> Actions,
    IReadOnlyList<ElementNode> Children);
