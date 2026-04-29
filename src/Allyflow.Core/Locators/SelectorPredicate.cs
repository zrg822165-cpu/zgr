namespace Allyflow.Core.Locators;

public sealed record SelectorPredicate(string Field, SelectorOperator Operator, string Value);
