using System;
using Zoh.Runtime.Execution;
using Zoh.Runtime.Parsing.Ast;
using Zoh.Runtime.Types;

namespace Zoh.Runtime.Verbs.Standard.Media;

public class SetVolumeDriver : IVerbDriver
{
    private readonly ISetVolumeHandler? _handler;
    public string? Namespace => "std";
    public string Name => "setVolume";

    public SetVolumeDriver(ISetVolumeHandler? handler = null)
    {
        _handler = handler;
    }

    public VerbResult Execute(IExecutionContext context, VerbCallAst call)
    {
        var ctx = context as Context;
        if (ctx == null) return VerbResult.Fatal(new Diagnostics.Diagnostic(Diagnostics.DiagnosticSeverity.Fatal, "invalid_context", "SetVolume requires a valid Context.", call.Start));

        string id = "";
        double volume = 1.0;

        if (call.UnnamedParams.Length > 0)
        {
            id = ValueResolver.Resolve(call.UnnamedParams[0], ctx).ToString();
        }

        if (call.UnnamedParams.Length > 1)
        {
            var vValue = ValueResolver.Resolve(call.UnnamedParams[1], ctx);
            if (vValue is ZohFloat f) volume = f.Value;
            else if (vValue is ZohInt i) volume = (double)i.Value;
            else if (double.TryParse(vValue.ToString(), out double d)) volume = d;
        }

        double fade = ResolveAttributeToDouble(call, "Fade", ctx) ?? 0;
        string easing = ResolveAttributeToString(call, "Easing", ctx) ?? "linear";

        var request = new SetVolumeRequest(id, volume, fade, easing);

        _handler?.OnSetVolume(ctx, request);

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
