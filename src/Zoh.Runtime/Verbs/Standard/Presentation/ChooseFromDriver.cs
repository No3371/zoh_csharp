using Zoh.Runtime.Execution;
using Zoh.Runtime.Parsing.Ast;
using Zoh.Runtime.Types;
using Zoh.Runtime.Diagnostics;
using System.Collections.Generic;
using System.Collections.Immutable;
using System;
using Zoh.Runtime.Verbs;

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

    public DriverResult Execute(IExecutionContext context, VerbCallAst call)
    {
        var ctx = context as Context;
        if (ctx == null) return DriverResult.Complete.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "invalid_context", "ChooseFrom requires a valid Context.", call.Start));

        string? speaker = ResolveAttributeToString(call, "By", ctx);
        string? portrait = ResolveAttributeToString(call, "Portrait", ctx);
        string style = ResolveAttributeToString(call, "Style", ctx) ?? "default";
        string? tag = ResolveAttributeToString(call, "tag", ctx);

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
                if (f.Value <= 0) return CreateTimeoutResult(call);
                timeoutMs = f.Value * 1000.0;
            }
            else if (tVal is ZohInt i)
            {
                if (i.Value <= 0) return CreateTimeoutResult(call);
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
                return DriverResult.Complete.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "invalid_type", "chooseFrom requires a list as its first argument.", call.Start));
            }
        }

        if (choices.Count == 0 && timeoutMs == null)
        {
            return new DriverResult.Complete(ZohValue.Nothing, ImmutableArray.Create(
                new Diagnostic(DiagnosticSeverity.Warning, "invalid_params", "ChooseFrom has no visible choices and no timeout.", call.Start)));
        }

        var request = new ChooseRequest(speaker, portrait, style, prompt, timeoutMs, choices, Tag: tag);

        if (_handler != null)
        {
            _handler.OnChooseFrom(ctx.Handle!, request);
            return new DriverResult.Suspend(new Continuation(
                new HostRequest(),
                outcome => outcome switch
                {
                    WaitCompleted c => DriverResult.Complete.Ok(c.Value),
                    WaitTimedOut => CreateTimeoutResult(call),
                    WaitCancelled wc => new DriverResult.Complete(ZohValue.Nothing, ImmutableArray.Create(
                        new Diagnostic(DiagnosticSeverity.Error, wc.Code, wc.Message, call.Start))),
                    _ => DriverResult.Complete.Ok()
                }
            ));
        }

        return DriverResult.Complete.Ok(ZohValue.Nothing);
    }

    private DriverResult CreateTimeoutResult(VerbCallAst call)
    {
        return new DriverResult.Complete(ZohValue.Nothing, ImmutableArray.Create(
            new Diagnostic(DiagnosticSeverity.Info, "timeout", "ChooseFrom timed out", call.Start)));
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
