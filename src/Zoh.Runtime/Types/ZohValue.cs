using System.Diagnostics.CodeAnalysis;

namespace Zoh.Runtime.Types;

/// <summary>
/// Base class for all ZOH runtime values.
/// Implemented as a sealed record hierarchy for exhaustive pattern matching.
/// </summary>
public abstract record ZohValue
{
    public abstract ZohValueType Type { get; }

    // Default clone returns this (shallow/reference copy)
    // Immutable types (Int, Float, String, Bool, Nothing) are safe to return this.
    // Mutable types (List, Map) must override.
    public virtual ZohValue DeepClone() => this;

    public static readonly ZohNothing Nothing = ZohNothing.Instance;

    public static ZohBool True => ZohBool.True;
    public static ZohBool False => ZohBool.False;

    public static ZohBool FromBool(bool value) => value ? True : False;
    public static ZohInt FromInt(long value) => new(value);
    public static ZohFloat FromFloat(double value) => new(value);
    public static ZohStr FromString(string value) => new(value);
}
