using Zoh.Runtime.Execution;
using Zoh.Runtime.Parsing.Ast;
using Zoh.Runtime.Types;
using Zoh.Runtime.Variables;
using Zoh.Runtime.Diagnostics;

namespace Zoh.Runtime.Verbs.Flow;

public class SleepDriver : IVerbDriver
{
    public string? Namespace => null;
    public string Name => "sleep";

    public VerbResult Execute(IExecutionContext context, VerbCallAst call)
    {
        var ctx = context as Context;
        if (ctx == null) return VerbResult.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "invalid_context", "Sleep requires a valid Context.", call.Start));

        if (call.UnnamedParams.Length != 1)
        {
            return VerbResult.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "arg_count", "Sleep requires 1 argument (duration in seconds).", call.Start));
        }

        var val = ValueResolver.Resolve(call.UnnamedParams[0], ctx);
        double durationSeconds = 0;

        if (val is ZohInt i) durationSeconds = i.Value;
        else if (val is ZohFloat f) durationSeconds = f.Value;
        else return VerbResult.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "invalid_arg", "Sleep duration must be a number.", call.Start));

        if (durationSeconds < 0) durationSeconds = 0;

        ctx.SetState(ContextState.Sleeping);
        // WaitCondition is DateTimeOffset of wake time (UTC)
        ctx.WaitCondition = DateTimeOffset.UtcNow.AddSeconds(durationSeconds);

        return VerbResult.Ok();
    }
}
