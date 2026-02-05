using Zoh.Runtime.Types;

namespace Zoh.Runtime.Types;

public static class ValueExtensions
{
    public static bool IsTruthy(this ZohValue value) => value switch
    {
        ZohNothing => false,
        ZohBool b => b.Value,
        ZohInt i => i.Value != 0,
        ZohFloat f => f.Value != 0.0,
        ZohStr s => !string.IsNullOrEmpty(s.Value),
        ZohList l => !l.Items.IsEmpty,
        ZohMap m => !m.Items.IsEmpty,
        ZohKvPair kv => true,
        _ => true
    };

    public static ZohInt AsInt(this ZohValue value) => value switch
    {
        ZohInt i => i,
        ZohFloat f => new ZohInt((long)f.Value), // Truncate toward zero
        ZohBool b => new ZohInt(b.Value ? 1 : 0),
        ZohStr s => long.TryParse(s.Value, out var l) ? new ZohInt(l) : throw new InvalidCastException($"Cannot parse string '{s.Value}' to Integer"),
        _ => throw new InvalidCastException($"Cannot cast {value.Type} to Integer")
    };

    public static ZohFloat AsFloat(this ZohValue value) => value switch
    {
        ZohFloat f => f,
        ZohInt i => new ZohFloat(i.Value),
        ZohBool b => new ZohFloat(b.Value ? 1.0 : 0.0),
        ZohStr s => double.TryParse(s.Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var d) ? new ZohFloat(d) : throw new InvalidCastException($"Cannot parse string '{s.Value}' to Float"),
        _ => throw new InvalidCastException($"Cannot cast {value.Type} to Float")
    };

    public static ZohStr AsString(this ZohValue value) => value switch
    {
        ZohStr s => s,
        ZohNothing => new ZohStr("?"),
        ZohBool b => new ZohStr(b.Value ? "true" : "false"),
        ZohInt i => new ZohStr(i.Value.ToString()),
        ZohFloat f => new ZohStr(f.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)),
        _ => new ZohStr(value.ToString() ?? "") // Fallback
    };

    public static IZohMap AsMap(this ZohValue value) => value is IZohMap m ? m : throw new InvalidCastException($"Cannot cast {value.Type} to Map");

    public static bool IsNothing(this ZohValue value) => value is ZohNothing;

    public static string GetTypeString(this ZohValue value) => value.Type switch
    {
        ZohValueType.Nothing => "nothing",
        ZohValueType.Boolean => "boolean",
        ZohValueType.Integer => "integer",
        ZohValueType.Float => "double",
        ZohValueType.String => "string",
        ZohValueType.List => "list",
        ZohValueType.Map => "map",
        ZohValueType.Channel => "channel",
        ZohValueType.Verb => "verb",
        ZohValueType.Expression => "expression",
        ZohValueType.Reference => "reference", // Spec doesn't strictly list 'reference' as return of Core.Type? 821 says "reference"? No, "string/integer.../expression/nothing/unknown". Wait 821 doesn't list reference. But 335 says "*reference Denoted as *variable_name". Core.Type usually resolves reference?
        // Spec 818: "Accept references. Return String...". If "var" is reference, context resolves it?
        // "In case of reference, the value is used" -> So we check the value's type.
        // A ZohValue CAN be a Reference if it's unresolved? But ValueResolver returns resolved values usually?
        // If it IS a ZohRef, it means it points to something?
        // ZohRef is internal maybe?
        // I will map ZohValueType.Reference to "reference" just in case, or "unknown".
        _ => "unknown"
    };

    public static bool IsTypeKeyword(string s) => s is "nothing" or "boolean" or "integer" or "double" or "string" or "list" or "map" or "channel" or "verb" or "expression";
}
