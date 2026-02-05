namespace Zoh.Runtime.Types;

public sealed record ZohChannel(string Name) : ZohValue
{
    public override ZohValueType Type => ZohValueType.Channel;
    public override string ToString() => $"<{Name}>";
}
