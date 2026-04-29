using Allyflow.Core.Refs;

namespace Allyflow.Protocol.Queries;

public sealed record FocusContextResult(
    WindowRef WindowRef,
    ElementRef? FocusRef,
    IReadOnlyList<ElementRef> ParentChain,
    IReadOnlyList<ElementRef> SiblingRefs,
    IReadOnlyList<ElementRef> ChildRefs,
    string SummaryText,
    IReadOnlyDictionary<string, string?> Diagnostics);
