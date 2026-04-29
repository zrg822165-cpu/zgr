using Allyflow.Core.Refs;

namespace Allyflow.Core.Actions;

public sealed record TargetResolution(
    string ResolutionSource,
    ElementRef? MatchedRef,
    WindowRef? WindowRef,
    string? SelectorUsed,
    double? Confidence,
    IReadOnlyDictionary<string, string?> Diagnostics);
