using Zoh.Runtime.Execution;
using Zoh.Runtime.Parsing.Ast;
using Zoh.Runtime.Types;
using Zoh.Runtime.Diagnostics;
using System.Collections.Generic;
using System;

namespace Zoh.Runtime.Verbs.Standard.Presentation;

public class ChooseFromDriver : IVerbDriver
{
    private readonly IChooseFromHandler? _handler;
    public string? Namespace => "std";
    public string Name => "chooseFrom";

    public ChooseFromDriver(IChooseFromHandler? handler = null)
    {
        _handler = handler;
    }

    public VerbResult Execute(IExecutionContext context, VerbCallAst call)
    {
        var ctx = context as Context;
        if (ctx == null) return VerbResult.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "invalid_context", "ChooseFrom requires a valid Context.", call.Start));

        string? speaker = ResolveAttributeToString(call, "By", ctx);
        string? portrait = ResolveAttributeToString(call, "Portrait", ctx);
        string style = ResolveAttributeToString(call, "Style", ctx) ?? "default";

        string? prompt = null;
        var promptAst = GetNamedParam(call, "prompt");
        if (promptAst != null)
        {
            var pVal = ValueResolver.Resolve(promptAst, ctx);
            if (pVal is ZohStr str) prompt = str.Value;
            else prompt = pVal.ToString();
        }

        double? timeoutMs = null;
        var timeoutAst = GetNamedParam(call, "timeout");
        if (timeoutAst != null)
        {
            var tVal = ValueResolver.Resolve(timeoutAst, ctx);
            if (tVal is ZohFloat f)
            {
                if (f.Value <= 0) return VerbResult.Ok(ZohValue.Nothing);
                timeoutMs = f.Value * 1000.0;
            }
            else if (tVal is ZohInt i)
            {
                if (i.Value <= 0) return VerbResult.Ok(ZohValue.Nothing);
                timeoutMs = i.Value * 1000.0;
            }
        }

        var choices = new List<ChoiceItem>();

        if (call.UnnamedParams.Length > 0)
        {
            var listVal = ValueResolver.Resolve(call.UnnamedParams[0], ctx);
            if (listVal is ZohList list)
            {
                foreach (var item in list.Items)
                {
                    if (item is IZohMap map)
                    {
                        string text = "";
                        ZohValue val = ZohValue.Nothing;
                        bool visible = true;

                        if (map.TryGet("text", out var t))
                            text = t is ZohStr s ? s.Value : t.ToString();

                        if (map.TryGet("value", out var v))
                            val = v;

                        if (map.TryGet("visible", out var vis))
                            visible = vis.IsTruthy();

                        if (visible)
                        {
                            choices.Add(new ChoiceItem(text, val));
                        }
                    }
                }
            }
            else
            {
                // Expected list, but didn't get one. Handled by generic error or ignored based on spec
                return VerbResult.Fatal(new Diagnostic(DiagnosticSeverity.Error, "type_error", "chooseFrom requires a list as its first argument.", call.Start));
            }
        }

        if (choices.Count == 0 && timeoutMs == null)
        {
            return VerbResult.Ok(ZohValue.Nothing);
        }

        var request = new ChooseRequest(speaker, portrait, style, prompt, timeoutMs, choices);

        if (_handler != null)
        {
            _handler.OnChooseFrom(ctx, request);
            return VerbResult.Yield(new HostContinuation("chooseFrom"));
        }

        return VerbResult.Ok(ZohValue.Nothing);
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
