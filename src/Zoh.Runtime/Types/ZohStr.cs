namespace Zoh.Runtime.Types;

public sealed record ZohStr(string Value) : ZohValue
{
    public override ZohValueType Type => ZohValueType.String;
    public override string ToString() => Value;
}
