using Zoh.Runtime.Execution;
using Zoh.Runtime.Parsing.Ast;
using Zoh.Runtime.Variables;
using Zoh.Runtime.Types;
using Zoh.Runtime.Diagnostics;

namespace Zoh.Runtime.Verbs.Core;

public class IncreaseDriver : IVerbDriver
{
    public string Namespace => "core";
    public string Name => "increase";

    public VerbResult Execute(IExecutionContext context, VerbCallAst verb)
    {
        // /increase *var [amount];
        // Default amount 1.

        return ModifyVariable(context, verb, 1);
    }

    internal static VerbResult ModifyVariable(IExecutionContext context, VerbCallAst verb, int sign)
    {
        string? targetName = null;
        System.Collections.Immutable.ImmutableArray<ValueAst> targetPath = System.Collections.Immutable.ImmutableArray<ValueAst>.Empty;
        ZohValue amount = new ZohInt(1);

        if (verb.UnnamedParams.Length > 0)
        {
            var p0 = verb.UnnamedParams[0];
            if (p0 is ValueAst.Reference r)
            {
                targetName = r.Name;
                targetPath = r.Path;
            }
            else if (p0 is ValueAst.String s) targetName = s.Value; // Allow explicit name
            else
            {
                var res = ValueResolver.Resolve(p0, context);
                if (res is ZohStr rs) targetName = rs.Value;
            }

            if (verb.UnnamedParams.Length > 1)
            {
                amount = ValueResolver.Resolve(verb.UnnamedParams[1], context);

                // Address GAP 2: Execute verb literal if provided
                if (amount is ZohVerb v)
                {
                    var execResult = context.ExecuteVerb(v.VerbValue, context);
                    if (execResult.IsFatal) return execResult;
                    amount = execResult.Value ?? ZohNothing.Instance;
                }

                // Address GAP 1: Strict numeric type validation
                if (!(amount is ZohInt || amount is ZohFloat))
                {
                    return VerbResult.Fatal(new Diagnostic(
                        DiagnosticSeverity.Fatal,
                        "invalid_type",
                        $"Amount parameter must evaluate to an integer or float, got {amount.Type}",
                        verb.Start));
                }
            }
        }

        if (targetName == null)
            return VerbResult.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "parameter_not_found", $"Usage: /{verb.Name} *var [amount]", verb.Start));

        var currentVal = Zoh.Runtime.Helpers.CollectionHelpers.GetAtPath(context, targetName, targetPath);

        // Coerce types
        if (currentVal is ZohInt i)
        {
            long delta = 1;
            if (amount is ZohInt ai) delta = ai.Value;
            else if (amount is ZohFloat af) delta = (long)af.Value;

            if (amount is ZohFloat af2)
            {
                var newVal = new ZohFloat(i.Value + (af2.Value * sign));
                return Zoh.Runtime.Helpers.CollectionHelpers.SetAtPath(context, targetName, targetPath, newVal);
            }

            var newInt = new ZohInt(i.Value + (delta * sign));
            return Zoh.Runtime.Helpers.CollectionHelpers.SetAtPath(context, targetName, targetPath, newInt);
        }
        else if (currentVal is ZohFloat f)
        {
            double delta = 1.0;
            if (amount is ZohInt ai) delta = ai.Value;
            else if (amount is ZohFloat af) delta = af.Value;

            var newVal = new ZohFloat(f.Value + (delta * sign));
            return Zoh.Runtime.Helpers.CollectionHelpers.SetAtPath(context, targetName, targetPath, newVal);
        }

        if (currentVal.IsNothing())
            return VerbResult.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "not_found", $"Variable or path not found or is Nothing", verb.Start));

        return VerbResult.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "invalid_type", $"Cannot {verb.Name} variable of type {currentVal.Type}", verb.Start));
    }
}
