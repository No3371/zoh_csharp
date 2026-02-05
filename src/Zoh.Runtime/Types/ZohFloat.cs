using System.Globalization;

namespace Zoh.Runtime.Types;

public sealed record ZohFloat(double Value) : ZohValue
{
    public override ZohValueType Type => ZohValueType.Float;
    public override string ToString()
    {
        if (double.IsPositiveInfinity(Value)) return "Infinity";
        if (double.IsNegativeInfinity(Value)) return "-Infinity";
        if (double.IsNaN(Value)) return "NaN";

        string s = Value.ToString(CultureInfo.InvariantCulture);
        if (!s.Contains(".") && !s.Contains("E") && !s.Contains("e"))
        {
            return s + ".0";
        }
        return s;
    }
}
