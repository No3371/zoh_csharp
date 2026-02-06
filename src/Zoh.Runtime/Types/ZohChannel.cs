namespace Zoh.Runtime.Types;

/// <summary>
/// Represents a channel reference value.
/// Generation tracks the generation at time of capture (0 = not captured yet).
/// </summary>
public sealed record ZohChannel(string Name, int Generation = 0) : ZohValue
{
    public override ZohValueType Type => ZohValueType.Channel;
    public override string ToString() => $"<{Name}>";

    /// <summary>
    /// Creates a new ZohChannel with the given generation.
    /// </summary>
    public ZohChannel WithGeneration(int generation) => this with { Generation = generation };
}
