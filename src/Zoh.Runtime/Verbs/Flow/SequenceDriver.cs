using System;
using Zoh.Runtime.Execution;
using Zoh.Runtime.Parsing.Ast;
using Zoh.Runtime.Types;
using Zoh.Runtime.Diagnostics;

namespace Zoh.Runtime.Verbs.Flow
{
    public class SequenceDriver : IVerbDriver
    {
        public string Namespace => "core.flow";
        public string Name => "sequence";

        public DriverResult Execute(IExecutionContext context, VerbCallAst call)
        {
            DriverResult lastResult = DriverResult.Complete.Ok();

            foreach (var param in call.UnnamedParams)
            {
                var breakResult = FlowUtils.EvaluateBreakIf(call, context);
                if (breakResult is DriverResult.Suspend) return breakResult;
                if (breakResult is { IsFatal: true }) return breakResult;
                if (breakResult != null) break;

                // Spec does not define continueif for sequence. Only breakif.

                var verbVal = ValueResolver.Resolve(param, context);
                if (!(verbVal is ZohVerb verbToRun))
                {
                    return DriverResult.Complete.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "invalid_type", $"Sequence item must be a verb. Got {verbVal}", call.Start));
                }

                lastResult = context.ExecuteVerb(verbToRun.VerbValue, context);
                if (lastResult.IsFatal) return lastResult;
            }

            return lastResult;
        }
    }
}
