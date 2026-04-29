using Allyflow.Core.Refs;

namespace Allyflow.Core.Models;

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
