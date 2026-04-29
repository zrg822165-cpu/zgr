namespace A11yFlow.Core.Refs;

public sealed record ElementRef(string Value)
{
    public override string ToString() => Value;
}
