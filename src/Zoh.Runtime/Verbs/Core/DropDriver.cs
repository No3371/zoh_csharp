using Zoh.Runtime.Execution;
using Zoh.Runtime.Parsing.Ast;
using Zoh.Runtime.Variables;
using Zoh.Runtime.Types;
using Zoh.Runtime.Diagnostics;

namespace Zoh.Runtime.Verbs.Core;

public class DropDriver : IVerbDriver
{
    public string Namespace => "core";
    public string Name => "drop";

    public VerbResult Execute(IExecutionContext context, VerbCallAst verb)
    {
        string? targetName = null;
        System.Collections.Immutable.ImmutableArray<ValueAst> targetPath = System.Collections.Immutable.ImmutableArray<ValueAst>.Empty;
        Scope? targetScope = null;

        foreach (var attr in verb.Attributes)
        {
            if (attr.Name.Equals("scope", StringComparison.OrdinalIgnoreCase))
            {
                var val = attr.Value != null ? ValueResolver.Resolve(attr.Value, context) : ZohValue.True;
                if (val is ZohStr s)
                {
                    if (s.Value.Equals("story", StringComparison.OrdinalIgnoreCase)) targetScope = Scope.Story;
                    else if (s.Value.Equals("context", StringComparison.OrdinalIgnoreCase)) targetScope = Scope.Context;
                }
            }
        }

        if (verb.UnnamedParams.Length > 0)
        {
            var p0 = verb.UnnamedParams[0];
            if (p0 is ValueAst.Reference r)
            {
                targetName = r.Name;
                targetPath = r.Path;
            }
            else
            {
                var res = ValueResolver.Resolve(p0, context);
                if (res is ZohStr rs) targetName = rs.Value;
                else return VerbResult.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "invalid_type", "Drop requires a variable reference", verb.Start));
            }
        }

        if (targetName == null)
            return VerbResult.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "parameter_not_found", "Usage: /drop *var", verb.Start));

        if (targetPath.IsEmpty)
        {
            context.Variables.Drop(targetName, targetScope);
            return VerbResult.Ok(ZohValue.Nothing);
        }
        else
        {
            return Zoh.Runtime.Helpers.CollectionHelpers.SetAtPath(context, targetName, targetPath, ZohValue.Nothing, targetScope);
        }
    }
}
