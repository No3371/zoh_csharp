namespace Zoh.Runtime.Types;

public sealed record ZohRef(string Name, ZohValue? Index = null) : ZohValue
{
    public override ZohValueType Type => ZohValueType.Reference;
    public override string ToString() => Index == null ? $"*{Name}" : $"*{Name}[{Index}]";
}
