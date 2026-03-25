using System;
using Zoh.Runtime.Execution;
using Zoh.Runtime.Parsing.Ast;
using Zoh.Runtime.Types;
using Zoh.Runtime.Diagnostics;
using Zoh.Runtime.Verbs;

namespace Zoh.Runtime.Verbs.Flow
{
    public class SwitchDriver : IVerbDriver
    {
        public string Namespace => "core.flow";
        public string Name => "switch";

        public DriverResult Execute(IExecutionContext context, VerbCallAst call)
        {
            if (call.UnnamedParams.Length < 1)
            {
                return DriverResult.Complete.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "parameter_not_found", "Use: /switch user_value, case1, action1, ...", call.Start));
            }

            var testValue = ValueResolver.Resolve(call.UnnamedParams[0], context);
            if (testValue is ZohVerb vSubject)
            {
                testValue = context.ExecuteVerb(vSubject.VerbValue, context).ValueOrNothing;
            }

            var remainingArgs = call.UnnamedParams.Length - 1;
            bool hasDefault = remainingArgs % 2 != 0;
            int pairCount = remainingArgs / 2;

            for (int i = 0; i < pairCount; i++)
            {
                int caseIndex = 1 + (i * 2);
                int actionIndex = caseIndex + 1;
                var caseValue = ValueResolver.Resolve(call.UnnamedParams[caseIndex], context);
                if (caseValue is ZohVerb caseVerb)
                {
                    var caseResult = context.ExecuteVerb(caseVerb.VerbValue, context);
                    if (caseResult is DriverResult.Suspend)
                        return caseResult;
                    if (caseResult.IsFatal)
                        return caseResult;
                    caseValue = caseResult.ValueOrNothing;
                }

                if (caseValue.Equals(testValue))
                {
                    var actionVal = ValueResolver.Resolve(call.UnnamedParams[actionIndex], context);
                    return DriverResult.Complete.Ok(actionVal);
                }
            }

            // 3. Default case
            if (hasDefault)
            {
                var defaultActionIndex = call.UnnamedParams.Length - 1;
                var defaultVal = ValueResolver.Resolve(call.UnnamedParams[defaultActionIndex], context);
                return DriverResult.Complete.Ok(defaultVal);
            }

            return DriverResult.Complete.Ok();
        }
    }
}
