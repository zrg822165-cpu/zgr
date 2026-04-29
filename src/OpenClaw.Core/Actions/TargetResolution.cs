using OpenClaw.Core.Refs;

namespace OpenClaw.Core.Actions;

public sealed record TargetResolution(
    string ResolutionSource,
    ElementRef? MatchedRef,
    WindowRef? WindowRef,
    string? SelectorUsed,
    double? Confidence,
    IReadOnlyDictionary<string, string?> Diagnostics);
