using System;
using Zoh.Runtime.Execution;
using Zoh.Runtime.Parsing.Ast;
using Zoh.Runtime.Types;
using Zoh.Runtime.Diagnostics;

namespace Zoh.Runtime.Verbs.Flow
{
    public class SwitchDriver : IVerbDriver
    {
        public string Namespace => "core";
        public string Name => "switch";

        public VerbResult Execute(IExecutionContext context, VerbCallAst call)
        {
            if (call.UnnamedParams.Length < 1)
            {
                return VerbResult.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "parameter_not_found", "Use: /switch user_value, case1, action1, ...", call.Start));
            }

            var testValue = ValueResolver.Resolve(call.UnnamedParams[0], context);
            if (testValue is ZohVerb vSubject)
            {
                testValue = context.ExecuteVerb(vSubject.VerbValue, context).Value;
            }

            var remainingArgs = call.UnnamedParams.Length - 1;
            bool hasDefault = remainingArgs % 2 != 0;
            int pairCount = remainingArgs / 2;

            for (int i = 0; i < pairCount; i++)
            {
                int caseIndex = 1 + (i * 2);
                int actionIndex = caseIndex + 1;
                var caseValue = ValueResolver.Resolve(call.UnnamedParams[caseIndex], context);

                if (caseValue.Equals(testValue))
                {
                    var actionVal = ValueResolver.Resolve(call.UnnamedParams[actionIndex], context);
                    return VerbResult.Ok(actionVal);
                }
            }

            // 3. Default case
            if (hasDefault)
            {
                var defaultActionIndex = call.UnnamedParams.Length - 1;
                var defaultVal = ValueResolver.Resolve(call.UnnamedParams[defaultActionIndex], context);
                return VerbResult.Ok(defaultVal);
            }

            return VerbResult.Ok();
        }
    }
}
