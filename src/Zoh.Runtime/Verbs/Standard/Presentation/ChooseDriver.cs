using Zoh.Runtime.Execution;
using Zoh.Runtime.Parsing.Ast;
using Zoh.Runtime.Types;
using Zoh.Runtime.Diagnostics;
using Zoh.Runtime.Lexing;
using Zoh.Runtime.Expressions;
using System.Collections.Generic;
using System;

namespace Zoh.Runtime.Verbs.Standard.Presentation;

public class ChooseDriver : IVerbDriver
{
    private readonly IChooseHandler? _handler;
    public string? Namespace => "std";
    public string Name => "choose";

    public ChooseDriver(IChooseHandler? handler = null)
    {
        _handler = handler;
    }

    public DriverResult Execute(IExecutionContext context, VerbCallAst call)
    {
        var ctx = context as Context;
        if (ctx == null) return DriverResult.Complete.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "invalid_context", "Choose requires a valid Context.", call.Start));

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
                if (f.Value <= 0) return DriverResult.Complete.Ok(ZohValue.Nothing);
                timeoutMs = f.Value * 1000.0;
            }
            else if (tVal is ZohInt i)
            {
                if (i.Value <= 0) return DriverResult.Complete.Ok(ZohValue.Nothing);
                timeoutMs = i.Value * 1000.0;
            }
        }

        var choices = new List<ChoiceItem>();
        var args = call.UnnamedParams;

        for (int i = 0; i < args.Length; i += 3)
        {
            if (i + 2 >= args.Length) break; // Should be enforced by Validator

            // Evaluate visibility
            var visVal = ValueResolver.Resolve(args[i], ctx);
            if (!visVal.IsTruthy()) continue;

            // Evaluate text
            var textVal = ValueResolver.Resolve(args[i + 1], ctx);
            if (textVal is ZohExpr textExpr)
            {
                var lexer = new Lexer(textExpr.ast.Source, false);
                var parser = new ExpressionParser(lexer.Tokenize().Tokens);
                textVal = ctx.Evaluator.Evaluate(parser.Parse());
            }

            string text = textVal is ZohStr s ? s.Value : textVal.ToString();

            // Evaluate value
            var valResult = ValueResolver.Resolve(args[i + 2], ctx);
            if (valResult is ZohExpr valExpr)
            {
                var lexer = new Lexer(valExpr.ast.Source, false);
                var parser = new ExpressionParser(lexer.Tokenize().Tokens);
                valResult = ctx.Evaluator.Evaluate(parser.Parse());
            }

            choices.Add(new ChoiceItem(text, valResult));
        }

        if (choices.Count == 0 && timeoutMs == null)
        {
            // According to spec, if no choices are visible and no timeout, it's a soft error or just returns nothing?
            // "If all evaluated choices are false, returns Nothing."
            return DriverResult.Complete.Ok(ZohValue.Nothing);
        }

        var request = new ChooseRequest(speaker, portrait, style, prompt, timeoutMs, choices, Tag: tag);

        if (_handler != null)
        {
            _handler.OnChoose(ctx.Handle!, request);
            return new DriverResult.Suspend(new Continuation(
                new HostRequest(),
                outcome => outcome switch
                {
                    WaitCompleted c => DriverResult.Complete.Ok(c.Value),
                    _ => DriverResult.Complete.Ok()
                }
            ));
        }

        return DriverResult.Complete.Ok(ZohValue.Nothing);
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
