using OpenClaw.Core.Refs;

namespace OpenClaw.Core.Locators;

public sealed record RefDescription(
    ElementRef Ref,
    WindowRef WindowRef,
    string Role,
    string? Name,
    string? AutomationId,
    string? ClassName,
    IReadOnlyList<string> States,
    IReadOnlyList<string> Actions,
    ElementRef? ParentRef,
    IReadOnlyList<ElementRef> ChildRefs,
    IReadOnlyDictionary<string, string?> Diagnostics);
