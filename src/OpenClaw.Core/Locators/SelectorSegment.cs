namespace OpenClaw.Core.Locators;

public sealed record SelectorSegment(
    string? Role,
    IReadOnlyList<SelectorPredicate> Predicates,
    TextSelector? Text);
