namespace Zoh.Runtime.Types;

public sealed record ZohInt(long Value) : ZohValue
{
    public override ZohValueType Type => ZohValueType.Integer;
    public override string ToString() => Value.ToString();
}
