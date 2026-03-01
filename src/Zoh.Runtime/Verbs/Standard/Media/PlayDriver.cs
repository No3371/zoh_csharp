using System;
using Zoh.Runtime.Execution;
using Zoh.Runtime.Parsing.Ast;
using Zoh.Runtime.Types;

namespace Zoh.Runtime.Verbs.Standard.Media;

public class PlayDriver : IVerbDriver
{
    private readonly IPlayHandler? _handler;
    public string? Namespace => "std";
    public string Name => "play";

    public PlayDriver(IPlayHandler? handler = null)
    {
        _handler = handler;
    }

    public DriverResult Execute(IExecutionContext context, VerbCallAst call)
    {
        var ctx = context as Context;
        if (ctx == null) return DriverResult.Complete.Fatal(new Diagnostics.Diagnostic(Diagnostics.DiagnosticSeverity.Fatal, "invalid_context", "Play requires a valid Context.", call.Start));

        string resource = "";
        if (call.UnnamedParams.Length > 0)
        {
            resource = ValueResolver.Resolve(call.UnnamedParams[0], ctx).ToString();
        }

        string id = ResolveAttributeToString(call, "Id", ctx) ?? resource;
        double volume = ResolveAttributeToDouble(call, "Volume", ctx) ?? 1.0;
        int loops = ResolveAttributeToInt(call, "Loops", ctx) ?? 1;
        string easing = ResolveAttributeToString(call, "Easing", ctx) ?? "linear";

        var request = new PlayRequest(resource, id, volume, loops, easing);

        _handler?.OnPlay(ctx, request);

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

    private int? ResolveAttributeToInt(VerbCallAst call, string name, Context ctx)
    {
        var attr = GetAttribute(call, name);
        if (attr != null && attr.Value != null)
        {
            var val = ValueResolver.Resolve(attr.Value, ctx);
            if (val is ZohInt i) return (int)i.Value;
            if (int.TryParse(val.ToString(), out int parsed)) return parsed;
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
