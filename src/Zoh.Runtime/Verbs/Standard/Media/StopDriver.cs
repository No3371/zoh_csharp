using System;
using Zoh.Runtime.Execution;
using Zoh.Runtime.Parsing.Ast;
using Zoh.Runtime.Types;

namespace Zoh.Runtime.Verbs.Standard.Media;

public class StopDriver : IVerbDriver
{
    private readonly IStopHandler? _handler;
    public string? Namespace => "std";
    public string Name => "stop";

    public StopDriver(IStopHandler? handler = null)
    {
        _handler = handler;
    }

    public VerbResult Execute(IExecutionContext context, VerbCallAst call)
    {
        var ctx = context as Context;
        if (ctx == null) return VerbResult.Fatal(new Diagnostics.Diagnostic(Diagnostics.DiagnosticSeverity.Fatal, "invalid_context", "Stop requires a valid Context.", call.Start));

        double fade = ResolveAttributeToDouble(call, "Fade", ctx) ?? 0;
        string easing = ResolveAttributeToString(call, "Easing", ctx) ?? "linear";

        string? id = null;
        if (call.UnnamedParams.Length > 0)
        {
            id = ValueResolver.Resolve(call.UnnamedParams[0], ctx).ToString();
        }

        var request = new StopRequest(id, fade, easing);

        _handler?.OnStop(ctx, request);

        return VerbResult.Ok();
    }

    private string? ResolveAttributeToString(VerbCallAst call, string name, Context ctx)
    {
        var attr = GetAttribute(call, name);
        if (attr != null && attr.Value != null)
        {
            var val = ValueResolver.Resolve(attr.Value, ctx);
            if (val is ZohStr str) return str.Value;
            return val.ToString();
        }
        return null;
    }

    private double? ResolveAttributeToDouble(VerbCallAst call, string name, Context ctx)
    {
        var attr = GetAttribute(call, name);
        if (attr != null && attr.Value != null)
        {
            var val = ValueResolver.Resolve(attr.Value, ctx);
            if (val is ZohFloat f) return f.Value;
            if (val is ZohInt i) return (double)i.Value;
            if (double.TryParse(val.ToString(), out double d)) return d;
        }
        return null;
    }

    private AttributeAst? GetAttribute(VerbCallAst call, string name)
    {
        foreach (var attr in call.Attributes)
        {
            if (attr.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                return attr;
        }
        return null;
    }
}
