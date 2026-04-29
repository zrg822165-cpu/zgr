using OpenClaw.Core.Models;
using OpenClaw.Core.Refs;

namespace OpenClaw.Protocol.Queries;

public sealed record ActiveWindowResult(
    WindowSummary Window,
    ElementRef? FocusRef,
    IReadOnlyList<string> ActionableSummary,
    string SummaryText,
    IReadOnlyDictionary<string, string?> Diagnostics);
