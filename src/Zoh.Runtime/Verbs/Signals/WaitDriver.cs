using System;
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
            return DriverResult.Complete.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "invalid_type", "Signal name must be a string.", call.Start));
        }

        string signalName = s.Value;

        double? timeoutMs = null;
        foreach (var param in call.NamedParams)
        {
            if (param.Key.Equals("timeout", StringComparison.OrdinalIgnoreCase))
            {
                var tVal = ValueResolver.Resolve(param.Value, ctx);
                if (tVal is ZohFloat f)
                {
                    if (f.Value <= 0)
                        return new DriverResult.Complete(ZohValue.Nothing, ImmutableArray.Create(
                            new Diagnostic(DiagnosticSeverity.Info, "timeout", "The timeout was reached.", call.Start)));
                    timeoutMs = f.Value * 1000.0;
                }
                else if (tVal is ZohInt i)
                {
                    if (i.Value <= 0)
                        return new DriverResult.Complete(ZohValue.Nothing, ImmutableArray.Create(
                            new Diagnostic(DiagnosticSeverity.Info, "timeout", "The timeout was reached.", call.Start)));
                    timeoutMs = i.Value * 1000.0;
                }
                break;
            }
        }

        return new DriverResult.Suspend(new Continuation(
            new SignalRequest(signalName, timeoutMs),
            outcome => outcome switch
            {
                WaitCompleted c => DriverResult.Complete.Ok(c.Value),
                WaitTimedOut => new DriverResult.Complete(ZohValue.Nothing, ImmutableArray.Create(
                    new Diagnostic(DiagnosticSeverity.Info, "timeout", "The timeout was reached.", call.Start))),
                WaitCancelled x => new DriverResult.Complete(
                    ZohNothing.Instance,
                    ImmutableArray.Create(new Diagnostic(DiagnosticSeverity.Error, x.Code, x.Message, call.Start))),
                _ => DriverResult.Complete.Ok()
            }
        ));
    }
}
