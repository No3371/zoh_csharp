using Zoh.Runtime.Execution;
using Zoh.Runtime.Parsing.Ast;
using Zoh.Runtime.Types;
using Zoh.Runtime.Variables;
using Zoh.Runtime.Diagnostics;
using Zoh.Runtime.Execution;
using Zoh.Runtime.Lexing;
using Zoh.Runtime.Expressions;
using System.Collections.Generic;

namespace Zoh.Runtime.Verbs.Standard.Presentation;

public class ConverseDriver : IVerbDriver
{
    private readonly IConverseHandler? _handler;
    public string? Namespace => "std";
    public string Name => "converse";

    public ConverseDriver(IConverseHandler? handler = null)
    {
        _handler = handler;
    }

    public DriverResult Execute(IExecutionContext context, VerbCallAst call)
    {
        var ctx = context as Context;
        if (ctx == null) return DriverResult.Complete.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "invalid_context", "Converse requires a valid Context.", call.Start));

        // Parse attributes
        string? speaker = ResolveAttributeToString(call, "By", ctx);
        string? portrait = ResolveAttributeToString(call, "Portrait", ctx);
        bool isAppend = HasAttribute(call, "Append");
        string style = ResolveAttributeToString(call, "Style", ctx) ?? "dialog";
        string? tag = ResolveAttributeToString(call, "tag", ctx);

        // Wait behavior: [Wait] attr > runtime flag 'interactive' > default true
        bool shouldWait = true;

        var waitAttr = GetAttribute(call, "Wait");
        if (waitAttr != null && waitAttr.Value != null)
        {
            var waitVal = ValueResolver.Resolve(waitAttr.Value, ctx);
            shouldWait = waitVal.IsTruthy();
        }
        else
        {
            // Fallback to interactive flag
            var interactiveVar = ctx.Variables.Get("interactive");
            if (interactiveVar is not ZohNothing)
            {
                shouldWait = interactiveVar.IsTruthy();
            }
        }

        // Parse timeout named param
        double? timeoutMs = null;
        var timeoutAst = GetNamedParam(call, "timeout");
        if (timeoutAst != null)
        {
            var tVal = ValueResolver.Resolve(timeoutAst, ctx);
            if (tVal is ZohFloat f)
            {
                if (f.Value <= 0) return DriverResult.Complete.Ok(); // Immediate timeout, per spec
                timeoutMs = f.Value * 1000.0; // Assume supplied as seconds, convert to ms
            }
            else if (tVal is ZohInt i)
            {
                if (i.Value <= 0) return DriverResult.Complete.Ok(); // Immediate timeout
                timeoutMs = i.Value * 1000.0;
            }
        }

        // Process Contents
        var contents = new List<string>();
        foreach (var param in call.UnnamedParams)
        {
            var contentVal = ValueResolver.Resolve(param, ctx);

            if (contentVal is ZohExpr expr)
            {
                var lexer = new Lexer(expr.ast.Source, false);
                var tokens = lexer.Tokenize().Tokens;
                var parser = new ExpressionParser(tokens);
                var expAst = parser.Parse();
                contentVal = ctx.Evaluator.Evaluate(expAst);
            }

            if (contentVal is ZohStr str)
            {
                // To evaluate interpolation on a plain string, we can construct an AST or see if ExpressionParser helps.
                // Wait, if it's already a ZohStr, it might have been passed as a literal string.
                // In Zoh, strings passed to verbs are interpolated during resolution if they are InterpolateExpressionAst.
                // ValueResolver handles this!
                // Wait, if we just do: `ValueResolver.Resolve(param, ctx)`, a literal string interpolation AST WILL be evaluated and returned as an interpolated ZohStr.
                // We DON'T need to re-interpolate!
                contents.Add(str.Value);
            }
            else
            {
                contents.Add(contentVal.ToString());
            }
        }

        if (contents.Count == 0)
        {
            return DriverResult.Complete.Ok(); // Nothing to say
        }

        var request = new ConverseRequest(speaker, portrait, isAppend, style, timeoutMs, contents, Tag: tag);

        if (_handler != null)
        {
            _handler.OnConverse(ctx.Handle!, request);

            if (shouldWait)
            {
                return new DriverResult.Suspend(new Continuation(
                    new HostRequest(),
                    outcome => outcome switch
                    {
                        WaitCompleted c => DriverResult.Complete.Ok(c.Value),
                        _ => DriverResult.Complete.Ok()
                    }
                ));
            }
        }

        return DriverResult.Complete.Ok();
    }

    private string? ResolveAttributeToString(VerbCallAst call, string name, Context ctx)
    {
        var attr = GetAttribute(call, name);
        if (attr != null && attr.Value != null)
        {
            var val = ValueResolver.Resolve(attr.Value, ctx);
            if (val is ZohStr str) return str.Value;
            return val.ToString(); // Fallback
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

    private bool HasAttribute(VerbCallAst call, string name)
    {
        return GetAttribute(call, name) != null;
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
