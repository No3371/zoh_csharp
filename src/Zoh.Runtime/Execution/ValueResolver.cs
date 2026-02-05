using Zoh.Runtime.Parsing.Ast;
using Zoh.Runtime.Types;
using Zoh.Runtime.Expressions;
using Zoh.Runtime.Lexing;
using System.Collections.Immutable;

namespace Zoh.Runtime.Execution;

public static class ValueResolver
{
    public static ZohValue Resolve(ValueAst ast, IExecutionContext context)
    {
        return ast switch
        {
            ValueAst.Nothing => ZohValue.Nothing,
            ValueAst.Boolean b => b.Value ? ZohValue.True : ZohValue.False,
            ValueAst.Integer i => new ZohInt(i.Value),
            ValueAst.Double d => new ZohFloat(d.Value),
            ValueAst.String s => new ZohStr(s.Value),
            ValueAst.Reference r => ResolveReference(r, context),
            ValueAst.Expression e => ResolveExpression(e, context),
            ValueAst.List l => ResolveList(l, context),
            ValueAst.Map m => ResolveMap(m, context),
            ValueAst.Verb v => new ZohVerb(v),
            ValueAst.Channel c => new ZohChannel(c.Name), // Channels are values? Or refs? Spec: channel is a value.
            _ => throw new NotImplementedException($"Unknown ValueAst: {ast.GetType().Name}")
        };
    }

    private static ZohValue ResolveReference(ValueAst.Reference r, IExecutionContext context)
    {
        // Use CollectionHelpers for path navigation
        return Zoh.Runtime.Helpers.CollectionHelpers.GetAtPath(context, r.Name, r.Path);
    }

    private static ZohValue ResolveExpression(ValueAst.Expression e, IExecutionContext context)
    {
        var lexer = new ExpressionLexer(e.Source, e.Position); // Position correct?
        // Note: ExpressionLexer needs absolute position? Or relative? 
        // TextPosition in e.Position is where the backtick started.
        // We might need better error reporting range.
        var tokens = lexer.Tokenize().Tokens;
        var parser = new ExpressionParser(tokens);
        var exprAst = parser.Parse();
        return context.Evaluator.Evaluate(exprAst);
    }

    private static ZohValue ResolveList(ValueAst.List l, IExecutionContext context)
    {
        var builder = ImmutableArray.CreateBuilder<ZohValue>();
        foreach (var item in l.Elements)
        {
            builder.Add(Resolve(item, context));
        }
        return new ZohList(builder.ToImmutable());
    }

    private static ZohValue ResolveMap(ValueAst.Map m, IExecutionContext context)
    {
        var builder = ImmutableDictionary.CreateBuilder<string, ZohValue>();
        foreach (var entry in m.Entries)
        {
            // Keys in Map AST are ValueAst. Assuming string for now, or resolving.
            // ZohMap keys are strings. 
            var keyVal = Resolve(entry.Key, context);
            string keyStr = keyVal is ZohStr ks ? ks.Value : keyVal.ToString(); // Or Force string? Spec?
            builder[keyStr] = Resolve(entry.Value, context);
        }
        return new ZohMap(builder.ToImmutable());
    }
}
