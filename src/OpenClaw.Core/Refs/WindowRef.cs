namespace OpenClaw.Core.Refs;

public sealed record WindowRef(string Value)
{
    public override string ToString() => Value;
}
