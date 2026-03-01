using System;
using Zoh.Runtime.Execution;
using Zoh.Runtime.Parsing.Ast;
using Zoh.Runtime.Types;
using Zoh.Runtime.Diagnostics;

namespace Zoh.Runtime.Verbs.Flow
{
    public class IfDriver : IVerbDriver
    {
        public string Namespace => "core";
        public string Name => "if";

        public DriverResult Execute(IExecutionContext context, VerbCallAst call)
        {
            if (call.UnnamedParams.Length < 2)
            {
                return DriverResult.Complete.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "parameter_not_found", "Use: /if condition, then_verb, [else_verb]", call.Start));
            }

            // 1. Evaluate Condition
            var conditionValue = ValueResolver.Resolve(call.UnnamedParams[0], context);
            bool isTrue = conditionValue.IsTruthy();

            // 2. Execute based on condition
            if (isTrue)
            {
                var param1 = call.UnnamedParams[1];
                var thenVal = ValueResolver.Resolve(param1, context);
                if (thenVal is ZohVerb thenVerb)
                {
                    return context.ExecuteVerb(thenVerb.VerbValue, context);
                }
                return DriverResult.Complete.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "invalid_type", "Current 'then' argument must be a verb.", call.Start));
            }
            else
            {
                if (call.UnnamedParams.Length >= 3)
                {
                    var param2 = call.UnnamedParams[2];
                    var elseVal = ValueResolver.Resolve(param2, context);
                    if (elseVal is ZohVerb elseVerb)
                    {
                        return context.ExecuteVerb(elseVerb.VerbValue, context);
                    }
                    return DriverResult.Complete.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "invalid_type", "Current 'else' argument must be a verb.", call.Start));
                }
            }

            return DriverResult.Complete.Ok();
        }
    }
}
