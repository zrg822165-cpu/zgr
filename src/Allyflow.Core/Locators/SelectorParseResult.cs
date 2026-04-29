namespace Allyflow.Core.Locators;

public sealed record SelectorParseResult(
    IReadOnlyList<ParsedSelector> Alternatives,
    SelectorParseError? Error)
{
    public bool IsSuccess => Error is null;
}
