using A11yFlow.Core.Refs;

namespace A11yFlow.Core.Actions;

public sealed record TargetResolution(
    string ResolutionSource,
    ElementRef? MatchedRef,
    WindowRef? WindowRef,
    string? SelectorUsed,
    double? Confidence,
    IReadOnlyDictionary<string, string?> Diagnostics);
