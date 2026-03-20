using System.Collections.Immutable;
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

    public DriverResult Execute(IExecutionContext context, VerbCallAst call)
    {
        var ctx = context as Context;
        if (ctx == null) return DriverResult.Complete.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "invalid_context", "Wait requires a valid Context.", call.Start));

        if (call.UnnamedParams.Length < 1)
        {
            return DriverResult.Complete.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "invalid_params", "Wait requires at least 1 argument (signal name).", call.Start));
        }

        var signalNameVal = ValueResolver.Resolve(call.UnnamedParams[0], ctx);
        if (signalNameVal is not ZohStr s)
        {
            return DriverResult.Complete.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "invalid_arg", "Signal name must be a string.", call.Start));
        }

        string signalName = s.Value;

        return new DriverResult.Suspend(new Continuation(
            new SignalRequest(signalName),
            outcome => outcome switch
            {
                WaitCompleted c => DriverResult.Complete.Ok(c.Value),
                WaitTimedOut => DriverResult.Complete.Ok(),
                WaitCancelled x => new DriverResult.Complete(
                    ZohNothing.Instance,
                    ImmutableArray.Create(new Diagnostic(DiagnosticSeverity.Error, x.Code, x.Message, call.Start))),
                _ => DriverResult.Complete.Ok()
            }
        ));
    }
}
