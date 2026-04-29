namespace A11yFlow.Core.Refs;

public sealed record WindowRef(string Value)
{
    public override string ToString() => Value;
}
