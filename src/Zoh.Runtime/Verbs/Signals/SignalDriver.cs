using Zoh.Runtime.Execution;
using Zoh.Runtime.Parsing.Ast;
using Zoh.Runtime.Types;
using Zoh.Runtime.Variables;
using Zoh.Runtime.Diagnostics;

namespace Zoh.Runtime.Verbs.Signals;

public class SignalDriver : IVerbDriver
{
    public string? Namespace => null;
    public string Name => "signal";

    public DriverResult Execute(IExecutionContext context, VerbCallAst call)
    {
        var ctx = context as Context;
        if (ctx == null) return DriverResult.Complete.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "invalid_context", "Signal requires a valid Context.", call.Start));

        if (call.UnnamedParams.Length < 1)
        {
            return DriverResult.Complete.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "invalid_params", "Signal requires at least 1 argument (signal name).", call.Start));
        }

        var signalNameVal = ValueResolver.Resolve(call.UnnamedParams[0], ctx);
        if (signalNameVal is not ZohStr s)
        {
            return DriverResult.Complete.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "invalid_arg", "Signal name must be a string.", call.Start));
        }

        string signalName = s.Value;
        ZohValue payload = ZohValue.Nothing;

        // Helper to resolve optional payload
        if (call.UnnamedParams.Length > 1)
        {
            payload = ValueResolver.Resolve(call.UnnamedParams[1], ctx);
        }

        // Broadcast
        int woken = ctx.SignalManager.Broadcast(signalName, payload);

        // Optional: Return the count of woken contexts?
        // The spec implies usage like `/signal "foo";`.
        // If capture is used: `-> *count;`.
        return DriverResult.Complete.Ok(new ZohInt(woken));
    }
}
