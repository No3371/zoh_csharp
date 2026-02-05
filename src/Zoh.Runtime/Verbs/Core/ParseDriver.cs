using System.Text.RegularExpressions;
using Zoh.Runtime.Execution;
using Zoh.Runtime.Parsing.Ast;
using Zoh.Runtime.Types;

namespace Zoh.Runtime.Verbs.Core;

public class ParseDriver : IVerbDriver
{
    public string Namespace => "core";
    public string Name => "parse";

    public VerbResult Execute(IExecutionContext context, VerbCallAst verb)
    {
        var paramsList = verb.UnnamedParams;
        if (paramsList.Length == 0)
        {
            return VerbResult.Fatal(new Diagnostics.Diagnostic(Diagnostics.DiagnosticSeverity.Error, "missing_param", "Usage: /parse value, [type];", verb.Start));
        }

        var value = ValueResolver.Resolve(paramsList[0], context);
        string str = value.AsString().Value;

        string targetType;
        if (paramsList.Length > 1)
        {
            targetType = ValueResolver.Resolve(paramsList[1], context).AsString().Value.ToLowerInvariant();
        }
        else
        {
            targetType = InferType(str);
        }

        try
        {
            return targetType switch
            {
                "integer" => VerbResult.Ok(new ZohInt(long.Parse(str))),
                "double" => VerbResult.Ok(new ZohFloat(double.Parse(str, System.Globalization.CultureInfo.InvariantCulture))),
                "boolean" => VerbResult.Ok(new ZohBool(bool.Parse(str))),
                "string" => VerbResult.Ok(new ZohStr(str)),
                "list" => VerbResult.Fatal(new Diagnostics.Diagnostic(Diagnostics.DiagnosticSeverity.Error, "not_implemented", "List parsing not yet supported", verb.Start)),
                "map" => VerbResult.Fatal(new Diagnostics.Diagnostic(Diagnostics.DiagnosticSeverity.Error, "not_implemented", "Map parsing not yet supported", verb.Start)),
                _ => VerbResult.Fatal(new Diagnostics.Diagnostic(Diagnostics.DiagnosticSeverity.Error, "invalid_type", $"Unknown type: {targetType}", verb.Start))
            };
        }
        catch (System.Exception ex) when (ex is FormatException || ex is OverflowException)
        {
            return VerbResult.Fatal(new Diagnostics.Diagnostic(Diagnostics.DiagnosticSeverity.Error, "invalid_format", $"Cannot parse '{str}' as {targetType}", verb.Start));
        }
    }

    private string InferType(string str)
    {
        if (long.TryParse(str, out _)) return "integer";
        if (double.TryParse(str, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out _)) return "double";
        if (bool.TryParse(str, out _)) return "boolean";
        if (str.TrimStart().StartsWith("[")) return "list";
        if (str.TrimStart().StartsWith("{")) return "map";
        return "string";
    }
}
