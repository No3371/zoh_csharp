using System;
using Zoh.Runtime.Execution;
using Zoh.Runtime.Parsing.Ast;
using Zoh.Runtime.Types;

namespace Zoh.Runtime.Verbs.Standard.Media;

public class ShowDriver : IVerbDriver
{
    private readonly IShowHandler? _handler;
    public string? Namespace => "std";
    public string Name => "show";

    public ShowDriver(IShowHandler? handler = null)
    {
        _handler = handler;
    }

    public DriverResult Execute(IExecutionContext context, VerbCallAst call)
    {
        var ctx = context as Context;
        if (ctx == null) return DriverResult.Complete.Fatal(new Diagnostics.Diagnostic(Diagnostics.DiagnosticSeverity.Fatal, "invalid_context", "Show requires a valid Context.", call.Start));

        // Resolve resource (first unnamed param)
        string resource = "";
        if (call.UnnamedParams.Length > 0)
        {
            resource = ValueResolver.Resolve(call.UnnamedParams[0], ctx).ToString();
        }

        // Build request from attributes
        string id = ResolveAttributeToString(call, "Id", ctx) ?? resource;
        double? rw = ResolveAttributeToDouble(call, "RW", ctx);
        double? rh = ResolveAttributeToDouble(call, "RH", ctx);
        double? width = ResolveAttributeToDouble(call, "Width", ctx);
        double? height = ResolveAttributeToDouble(call, "Height", ctx);
        string anchor = ResolveAttributeToString(call, "Anchor", ctx) ?? "center";
        double posX = ResolveAttributeToDouble(call, "PosX", ctx) ?? 0.5;
        double posY = ResolveAttributeToDouble(call, "PosY", ctx) ?? 0.5;
        double posZ = ResolveAttributeToDouble(call, "PosZ", ctx) ?? 0;
        double fade = ResolveAttributeToDouble(call, "Fade", ctx) ?? 0;
        double opacity = ResolveAttributeToDouble(call, "Opacity", ctx) ?? 1.0;
        string easing = ResolveAttributeToString(call, "Easing", ctx) ?? "linear";
        string? tag = ResolveAttributeToString(call, "tag", ctx);

        var request = new ShowRequest(
            resource, id, rw, rh, width, height,
            anchor, posX, posY, posZ, fade, opacity, easing,
            Tag: tag);

        _handler?.OnShow(ctx, request);

        return DriverResult.Complete.Ok(new ZohStr(id));
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
