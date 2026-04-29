namespace A11yFlow.Core.Locators;

public sealed record SelectorSegment(
    string? Role,
    IReadOnlyList<SelectorPredicate> Predicates,
    TextSelector? Text);
