using Zoh.Runtime.Parsing.Ast;
using Zoh.Runtime.Types; // Self reference if needed, usually redundant if namespace match
using System.Text; // For StringBuilder

namespace Zoh.Runtime.Types;

public sealed record ZohVerb(ValueAst.Verb VerbValue) : ZohValue
{
    // Wrapping ValueAst.Verb which contains VerbCallAst
    public override ZohValueType Type => ZohValueType.Verb;

    // Helper to get the actual AST node if needed
    public ValueAst VerbAst => VerbValue;

    public override string ToString()
    {
        var sb = new StringBuilder("/");
        var call = VerbValue.Call;
        if (!string.IsNullOrEmpty(call.Namespace))
        {
            sb.Append(call.Namespace).Append('.');
        }
        sb.Append(call.Name);

        // Attributes
        foreach (var attr in call.Attributes)
        {
            sb.Append(" [").Append(attr.Name);
            if (attr.Value != null && attr.Value is not ValueAst.Nothing)
            {
                sb.Append(':').Append(AstToString(attr.Value));
            }
            sb.Append(']');
        }

        bool first = true;

        // Named Params
        if (call.NamedParams.Count > 0)
        {
            foreach (var kvp in call.NamedParams)
            {
                if (!first) sb.Append(", "); else sb.Append(' ');
                sb.Append(kvp.Key).Append(':').Append(AstToString(kvp.Value));
                first = false;
            }
        }

        // Unnamed Params
        if (call.UnnamedParams.Length > 0)
        {
            foreach (var p in call.UnnamedParams)
            {
                if (!first) sb.Append(", "); else sb.Append(' ');
                sb.Append(AstToString(p));
                first = false;
            }
        }

        sb.Append(';');
        return sb.ToString();
    }

    private static string AstToString(ValueAst ast)
    {
        return ast switch
        {
            ValueAst.Nothing => "?",
            ValueAst.Boolean b => b.Value ? "true" : "false",
            ValueAst.Integer i => i.Value.ToString(),
            ValueAst.Double d => new ZohFloat(d.Value).ToString(),
            ValueAst.String s => $"\"{s.Value}\"",
            ValueAst.Reference r => RefToString(r),
            ValueAst.Expression e => $"`{e.Source}`",
            ValueAst.List l => ListToString(l),
            ValueAst.Map m => MapToString(m),
            ValueAst.Verb v => new ZohVerb(v).ToString(),
            ValueAst.Channel c => $"<{c.Name}>",
            _ => "?"
        };
    }

    private static string RefToString(ValueAst.Reference r)
    {
        var sb = new StringBuilder("*");
        sb.Append(r.Name);
        foreach (var idx in r.Path)
        {
            sb.Append('[').Append(AstToString(idx)).Append(']');
        }
        return sb.ToString();
    }

    private static string ListToString(ValueAst.List l)
    {
        var sb = new StringBuilder("[");
        for (int i = 0; i < l.Elements.Length; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append(AstToString(l.Elements[i]));
        }
        sb.Append("]");
        return sb.ToString();
    }

    private static string MapToString(ValueAst.Map m)
    {
        var sb = new StringBuilder("{");
        bool first = true;
        foreach (var kvp in m.Entries)
        {
            if (!first) sb.Append(", ");
            sb.Append(AstToString(kvp.Key)).Append(": ").Append(AstToString(kvp.Value));
            first = false;
        }
        sb.Append("}");
        return sb.ToString();
    }

    // Constructor from VerbCallAst creates a simplified ValueAst.Verb container
    // Note: ValueAst.Verb takes (VerbCallAst Call)
    public ZohVerb(VerbCallAst verbCall) : this(new ValueAst.Verb(verbCall)) { }

    public static ZohVerb FromAst(VerbCallAst ast) => new ZohVerb(ast);
}
