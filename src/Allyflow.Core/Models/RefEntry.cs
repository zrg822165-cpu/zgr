using Allyflow.Core.Refs;

namespace Allyflow.Core.Models;

public sealed record RefEntry(
    string Ref,
    WindowRef WindowRef,
    string BackendSource,
    object AutomationElement,
    string? SelectorHint,
    string SnapshotVersion,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastValidatedAt);
