using Zoh.Runtime.Execution;
using Zoh.Runtime.Parsing.Ast;
using Zoh.Runtime.Types;
using Zoh.Runtime.Variables;
using Zoh.Runtime.Diagnostics;

namespace Zoh.Runtime.Verbs.Signals;

public class WaitDriver : IVerbDriver
{
    public string? Namespace => null;
    public string Name => "wait";

    public VerbResult Execute(IExecutionContext context, VerbCallAst call)
    {
        var ctx = context as Context;
        if (ctx == null) return VerbResult.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "invalid_context", "Wait requires a valid Context.", call.Start));

        if (call.UnnamedParams.Length < 1)
        {
            return VerbResult.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "arg_count", "Wait requires at least 1 argument (signal name).", call.Start));
        }

        var signalNameVal = ValueResolver.Resolve(call.UnnamedParams[0], ctx);
        if (signalNameVal is not ZohStr s)
        {
            return VerbResult.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "invalid_arg", "Signal name must be a string.", call.Start));
        }

        string signalName = s.Value;

        // Subscribe
        ctx.SignalManager.Subscribe(signalName, ctx);

        // Set state
        ctx.SetState(ContextState.WaitingMessage);
        ctx.WaitCondition = signalName; // Set simple string for now as per out-of-scope timeout

        return VerbResult.Ok();
    }
}
