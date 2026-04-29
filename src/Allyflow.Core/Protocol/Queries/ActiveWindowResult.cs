using Allyflow.Core.Models;
using Allyflow.Core.Refs;

namespace Allyflow.Protocol.Queries;

public sealed record ActiveWindowResult(
    WindowSummary Window,
    ElementRef? FocusRef,
    IReadOnlyList<string> ActionableSummary,
    string SummaryText,
    IReadOnlyDictionary<string, string?> Diagnostics);
