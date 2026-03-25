using System.Collections.Immutable;
using Zoh.Runtime.Execution;
using Zoh.Runtime.Parsing.Ast;
using Zoh.Runtime.Types;

namespace Zoh.Runtime.Verbs.Collection;

public class FirstDriver : IVerbDriver
{
    public string Namespace => "core.collection";
    public string Name => "first";

    public DriverResult Execute(IExecutionContext context, VerbCallAst verb)
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

            if (value is ZohVerb vSubject)
            {
                value = context.ExecuteVerb(vSubject.VerbValue, context).ValueOrNothing;
            }
            else if (value is ZohExpr expr)
            {
                value = ValueResolver.Resolve(expr.ast, context);
            }

            if (!value.IsNothing())
            {
                return DriverResult.Complete.Ok(value);
            }
        }

        return DriverResult.Complete.Ok(ZohValue.Nothing);
    }
}
