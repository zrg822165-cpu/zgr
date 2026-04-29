using A11yFlow.Core.Refs;

namespace A11yFlow.Protocol.Queries;

public sealed record FocusContextResult(
    WindowRef WindowRef,
    ElementRef? FocusRef,
    IReadOnlyList<ElementRef> ParentChain,
    IReadOnlyList<ElementRef> SiblingRefs,
    IReadOnlyList<ElementRef> ChildRefs,
    string SummaryText,
    IReadOnlyDictionary<string, string?> Diagnostics);
