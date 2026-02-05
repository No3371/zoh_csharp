using Zoh.Runtime.Execution;
using Zoh.Runtime.Parsing.Ast;
using Zoh.Runtime.Variables;
using Zoh.Runtime.Types;
using Zoh.Runtime.Diagnostics;

namespace Zoh.Runtime.Verbs.Core;

public class GetDriver : IVerbDriver
{
    public string Namespace => "core";
    public string Name => "get";

    public VerbResult Execute(IExecutionContext context, VerbCallAst verb)
    {
        string? targetName = null;
        System.Collections.Immutable.ImmutableArray<ValueAst> targetPath = System.Collections.Immutable.ImmutableArray<ValueAst>.Empty;
        bool required = false;

        foreach (var attr in verb.Attributes)
        {
            if (attr.Name.Equals("required", StringComparison.OrdinalIgnoreCase))
            {
                required = true;
            }
        }

        if (verb.UnnamedParams.Length > 0)
        {
            var p0 = verb.UnnamedParams[0];
            if (p0 is ValueAst.Reference refAst)
            {
                targetName = refAst.Name;
                targetPath = refAst.Path;
            }
            else
            {
                var resolvedKey = ValueResolver.Resolve(p0, context);
                if (resolvedKey is ZohStr s) targetName = s.Value;
                else return VerbResult.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "invalid_type", "Get requires a variable reference or name string", verb.Start));
            }
        }

        if (targetName == null)
            return VerbResult.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "parameter_not_found", "Usage: /get *var", verb.Start));

        var val = Zoh.Runtime.Helpers.CollectionHelpers.GetAtPath(context, targetName, targetPath);

        if (required && val.IsNothing())
        {
            return VerbResult.Error(ZohValue.Nothing, new Diagnostic(DiagnosticSeverity.Error, "not_found", $"Required variable '{targetName}' not found", verb.Start));
        }

        return VerbResult.Ok(val);
    }
}
