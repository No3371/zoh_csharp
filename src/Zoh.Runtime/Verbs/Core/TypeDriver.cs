using Zoh.Runtime.Execution;
using Zoh.Runtime.Parsing.Ast;
using Zoh.Runtime.Variables;
using Zoh.Runtime.Types;
using Zoh.Runtime.Diagnostics;

namespace Zoh.Runtime.Verbs.Core;

public class TypeDriver : IVerbDriver
{
    public string Namespace => "core";
    public string Name => "type";

    public VerbResult Execute(IExecutionContext context, VerbCallAst verb)
    {
        // /type value;
        // returns string representation of type.

        if (verb.UnnamedParams.Length == 0)
            return VerbResult.Error(ZohValue.Nothing, new Diagnostic(DiagnosticSeverity.Error, "MissingArguments", "Usage: /type value", verb.Start));

        var val = ValueResolver.Resolve(verb.UnnamedParams[0], context);

        var typeStr = val.Type switch
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
            ZohValueType.Reference => "reference", // Not in spec list but consistent with spec casing
            _ => "unknown"
        };

        return VerbResult.Ok(new ZohStr(typeStr));
    }
}
