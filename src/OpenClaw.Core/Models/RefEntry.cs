using OpenClaw.Core.Refs;

namespace OpenClaw.Core.Models;

public sealed record RefEntry(
    string Ref,
    WindowRef WindowRef,
    string BackendSource,
    object AutomationElement,
    string? SelectorHint,
    string SnapshotVersion,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastValidatedAt);
