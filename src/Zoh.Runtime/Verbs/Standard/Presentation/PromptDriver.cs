using Zoh.Runtime.Execution;
using Zoh.Runtime.Parsing.Ast;
using Zoh.Runtime.Types;
using Zoh.Runtime.Diagnostics;
using System;

namespace Zoh.Runtime.Verbs.Standard.Presentation;

public class PromptDriver : IVerbDriver
{
    private readonly IPromptHandler? _handler;
    public string? Namespace => "std";
    public string Name => "prompt";

    public PromptDriver(IPromptHandler? handler = null)
    {
        _handler = handler;
    }

    public DriverResult Execute(IExecutionContext context, VerbCallAst call)
    {
        var ctx = context as Context;
        if (ctx == null) return DriverResult.Complete.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "invalid_context", "Prompt requires a valid Context.", call.Start));

        string style = ResolveAttributeToString(call, "Style", ctx) ?? "default";

        string? promptText = null;
        if (call.UnnamedParams.Length > 0)
        {
            var pVal = ValueResolver.Resolve(call.UnnamedParams[0], ctx);
            if (pVal is ZohStr str) promptText = str.Value; // already interpolated if passed natively
            else promptText = pVal.ToString();
        }

        double? timeoutMs = null;
        var timeoutAst = GetNamedParam(call, "timeout");
        if (timeoutAst != null)
        {
            var tVal = ValueResolver.Resolve(timeoutAst, ctx);
            if (tVal is ZohFloat f)
            {
                if (f.Value <= 0) return DriverResult.Complete.Ok(new ZohStr(""));
                timeoutMs = f.Value * 1000.0;
            }
            else if (tVal is ZohInt i)
            {
                if (i.Value <= 0) return DriverResult.Complete.Ok(new ZohStr(""));
                timeoutMs = i.Value * 1000.0;
            }
        }

        var request = new PromptRequest(style, promptText, timeoutMs);

        if (_handler != null)
        {
            _handler.OnPrompt(ctx.Handle!, request);
            return new DriverResult.Suspend(new Continuation(
                new HostRequest(),
                outcome => outcome switch
                {
                    WaitCompleted c => DriverResult.Complete.Ok(c.Value),
                    _ => DriverResult.Complete.Ok()
                }
            ));
        }

        return DriverResult.Complete.Ok(new ZohStr(""));
    }

    private string? ResolveAttributeToString(VerbCallAst call, string name, Context ctx)
    {
        foreach (var attr in call.Attributes)
        {
            if (attr.Name.Equals(name, StringComparison.OrdinalIgnoreCase) && attr.Value != null)
            {
                var val = ValueResolver.Resolve(attr.Value, ctx);
                if (val is ZohStr str) return str.Value;
                return val.ToString();
            }
        }
        return null;
    }

    private ValueAst? GetNamedParam(VerbCallAst call, string name)
    {
        foreach (var param in call.NamedParams)
        {
            if (param.Key.Equals(name, StringComparison.OrdinalIgnoreCase))
                return param.Value;
        }
        return null;
    }
}
