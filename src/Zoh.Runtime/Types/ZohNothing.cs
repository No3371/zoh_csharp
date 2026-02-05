namespace Zoh.Runtime.Types;

public sealed record ZohNothing : ZohValue
{
    internal ZohNothing() { }
    public static readonly ZohNothing Instance = new();
    public override ZohValueType Type => ZohValueType.Nothing;
    public override string ToString() => "?";
}
