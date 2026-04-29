using A11yFlow.Core.Models;
using A11yFlow.Core.Refs;

namespace A11yFlow.Protocol.Queries;

public sealed record ActiveWindowResult(
    WindowSummary Window,
    ElementRef? FocusRef,
    IReadOnlyList<string> ActionableSummary,
    string SummaryText,
    IReadOnlyDictionary<string, string?> Diagnostics);
