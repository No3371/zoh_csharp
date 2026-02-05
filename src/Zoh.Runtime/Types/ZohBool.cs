namespace Zoh.Runtime.Types;

public sealed record ZohBool(bool Value) : ZohValue
{
    public override ZohValueType Type => ZohValueType.Boolean;
    public override string ToString() => Value ? "true" : "false"; // Lowercase as per spec

    public new static readonly ZohBool True = new(true);
    public new static readonly ZohBool False = new(false);
}
