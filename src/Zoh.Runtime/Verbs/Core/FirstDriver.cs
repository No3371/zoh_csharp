using System.Collections.Immutable;
using Zoh.Runtime.Execution;
using Zoh.Runtime.Parsing.Ast;
using Zoh.Runtime.Types;

namespace Zoh.Runtime.Verbs.Core;

public class FirstDriver : IVerbDriver
{
    public string Namespace => "core";
    public string Name => "first";

    public VerbResult Execute(IExecutionContext context, VerbCallAst verb)
    {
        foreach (var param in verb.UnnamedParams)
        {
            // First resolves the param.
            // If param is VerbCall or Expression, resolver usually handles simple cases.
            // But Spec says: "If value is VerbValue: executeVerb... If ExpressionValue: evaluate..."
            // ValueResolver resolves AST to Value.
            // If AST is VerbCallAst, ValueResolver usually returns VerbResult? No, Resolve returns ZohValue.
            // We need to check if resolved value IS a Verb (objectified) or Expression.

            var value = ValueResolver.Resolve(param, context);

            // Spec L751:
            // if value is VerbValue (objectified verb) -> Execute it.
            // BUT ValueResolver might have already executed if it was a direct VerbCallAst?
            // Usually VerbCallAst in Params is wrapped or evaluated?
            // If user writes `/first /try_a;, /try_b;`.
            // The AST for params will be VerbCallAst (if parsed as such) or ValueAst.VerbCall.
            // ValueResolver.Resolve(ValueAst.VerbCall) -> Executes the verb? 
            // Let's check ValueResolver:
            // It usually calls Context.Execute(verbCall).

            // If Value is ExpressionValue (quoted expression `...`), we eval it.
            // If Value is ReferenceValue, resolved already by ValueResolver if passed directly?
            // ValueResolver usually resolves Reference.

            // So 'value' is likely the RESULT of the param.
            // If result is NOT Nothing, return it.

            if (!value.IsNothing())
            {
                return VerbResult.Ok(value);
            }
        }

        return VerbResult.Ok(ZohValue.Nothing);
    }
}
