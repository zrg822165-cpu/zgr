namespace OpenClaw.Core.Refs;

public sealed record ElementRef(string Value)
{
    public override string ToString() => Value;
}
