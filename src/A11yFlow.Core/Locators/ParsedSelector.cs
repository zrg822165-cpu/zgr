namespace A11yFlow.Core.Locators;

public sealed record ParsedSelector(
    SelectorScope Scope,
    IReadOnlyList<SelectorSegment> Segments,
    IReadOnlyList<SelectorRelation> Relations,
    string SourceText);
