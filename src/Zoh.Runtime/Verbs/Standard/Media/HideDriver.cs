using System;
using Zoh.Runtime.Execution;
using Zoh.Runtime.Parsing.Ast;
using Zoh.Runtime.Types;

namespace Zoh.Runtime.Verbs.Standard.Media;

public class HideDriver : IVerbDriver
{
    private readonly IHideHandler? _handler;
    public string? Namespace => "std";
    public string Name => "hide";

    public HideDriver(IHideHandler? handler = null)
    {
        _handler = handler;
    }

    public DriverResult Execute(IExecutionContext context, VerbCallAst call)
    {
        var ctx = context as Context;
        if (ctx == null) return DriverResult.Complete.Fatal(new Diagnostics.Diagnostic(Diagnostics.DiagnosticSeverity.Fatal, "invalid_context", "Hide requires a valid Context.", call.Start));

        string id = "";
        if (call.UnnamedParams.Length > 0)
        {
            id = ValueResolver.Resolve(call.UnnamedParams[0], ctx).ToString();
        }

        double fade = ResolveAttributeToDouble(call, "Fade", ctx) ?? 0;
        string easing = ResolveAttributeToString(call, "Easing", ctx) ?? "linear";

        var request = new HideRequest(id, fade, easing);

        _handler?.OnHide(ctx, request);

        return DriverResult.Complete.Ok();
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
