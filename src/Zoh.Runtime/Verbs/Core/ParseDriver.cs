using System.Collections.Immutable;
using System.Text.Json;
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
        string str = value.AsString().Value.Trim();

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
                "list" => ParseList(str, verb),
                "map" => ParseMap(str, verb),
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
        if (str.StartsWith("[")) return "list";
        if (str.StartsWith("{")) return "map";
        return "string";
    }

    private VerbResult ParseList(string str, VerbCallAst verb)
    {
        try
        {
            using var doc = JsonDocument.Parse(str);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                return VerbResult.Fatal(new Diagnostics.Diagnostic(
                    Diagnostics.DiagnosticSeverity.Error,
                    "invalid_format",
                    $"Cannot parse '{str}' as list: not a JSON array",
                    verb.Start));
            }

            var items = doc.RootElement
                .EnumerateArray()
                .Select(JsonElementToZohValue)
                .ToImmutableArray();

            return VerbResult.Ok(new ZohList(items));
        }
        catch (JsonException)
        {
            return VerbResult.Fatal(new Diagnostics.Diagnostic(
                Diagnostics.DiagnosticSeverity.Error,
                "invalid_format",
                $"Cannot parse '{str}' as list: malformed JSON",
                verb.Start));
        }
    }

    private VerbResult ParseMap(string str, VerbCallAst verb)
    {
        try
        {
            using var doc = JsonDocument.Parse(str);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return VerbResult.Fatal(new Diagnostics.Diagnostic(
                    Diagnostics.DiagnosticSeverity.Error,
                    "invalid_format",
                    $"Cannot parse '{str}' as map: not a JSON object",
                    verb.Start));
            }

            var builder = ImmutableDictionary.CreateBuilder<string, ZohValue>();
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                builder[prop.Name] = JsonElementToZohValue(prop.Value);
            }

            return VerbResult.Ok(new ZohMap(builder.ToImmutable()));
        }
        catch (JsonException)
        {
            return VerbResult.Fatal(new Diagnostics.Diagnostic(
                Diagnostics.DiagnosticSeverity.Error,
                "invalid_format",
                $"Cannot parse '{str}' as map: malformed JSON",
                verb.Start));
        }
    }

    private static ZohValue JsonElementToZohValue(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Null => ZohValue.Nothing,
        JsonValueKind.True => new ZohBool(true),
        JsonValueKind.False => new ZohBool(false),
        JsonValueKind.Number when element.TryGetInt64(out var intValue) => new ZohInt(intValue),
        JsonValueKind.Number => new ZohFloat(element.GetDouble()),
        JsonValueKind.String => new ZohStr(element.GetString() ?? string.Empty),
        JsonValueKind.Array => new ZohList(
            element.EnumerateArray()
                .Select(JsonElementToZohValue)
                .ToImmutableArray()),
        JsonValueKind.Object => new ZohMap(
            element.EnumerateObject()
                .ToImmutableDictionary(
                    property => property.Name,
                    property => JsonElementToZohValue(property.Value))),
        _ => ZohValue.Nothing
    };
}
