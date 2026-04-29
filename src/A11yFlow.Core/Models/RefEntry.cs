using A11yFlow.Core.Refs;

namespace A11yFlow.Core.Models;

public sealed record RefEntry(
    string Ref,
    WindowRef WindowRef,
    string BackendSource,
    object AutomationElement,
    string? SelectorHint,
    string SnapshotVersion,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastValidatedAt);
