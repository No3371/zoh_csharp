using System;
using Zoh.Runtime.Execution;
using Zoh.Runtime.Parsing.Ast;
using Zoh.Runtime.Types;
using Zoh.Runtime.Diagnostics;

namespace Zoh.Runtime.Verbs.Flow
{
    public class LoopDriver : IVerbDriver
    {
        public string Namespace => "core";
        public string Name => "loop";

        public DriverResult Execute(IExecutionContext context, VerbCallAst call)
        {
            if (call.UnnamedParams.Length < 2)
            {
                return DriverResult.Complete.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "parameter_not_found", "Use: /loop times, verb", call.Start));
            }

            var iterationsVal = ValueResolver.Resolve(call.UnnamedParams[0], context);
            if (iterationsVal.Type != ZohValueType.Integer)
            {
                return DriverResult.Complete.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "invalid_type", "Iterations must be an integer.", call.Start));
            }
            long iterations = ((ZohInt)iterationsVal).Value;

            var verbVal = ValueResolver.Resolve(call.UnnamedParams[1], context);
            if (!(verbVal is ZohVerb verbToRun))
            {
                return DriverResult.Complete.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "invalid_type", "Second argument must be a verb.", call.Start));
            }

            long count = 0;
            bool isInfinite = iterations == -1;

            while (isInfinite || count < iterations)
            {
                if (FlowUtils.ShouldBreak(call, context))
                {
                    break;
                }

                var result = context.ExecuteVerb(verbToRun.VerbValue, context);
                if (result.IsFatal) return result;

                count++;
            }

            return DriverResult.Complete.Ok();
        }
    }
}
