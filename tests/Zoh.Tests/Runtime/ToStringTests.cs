using System.Collections.Immutable;
using Xunit;
using Zoh.Runtime.Execution;
using Zoh.Runtime.Types;
using Zoh.Tests.Execution;

namespace Zoh.Tests.Runtime;

public class ToStringTests
{
    private readonly TestExecutionContext _context;

    public ToStringTests()
    {
        _context = new TestExecutionContext();
    }

    [Fact]
    public void Nothing_ToString_ReturnsQuestionMark()
    {
        Assert.Equal("?", ZohValue.Nothing.ToString());
    }

    [Fact]
    public void Bool_ToString_ReturnsLowercase()
    {
        Assert.Equal("true", ZohBool.True.ToString());
        Assert.Equal("false", ZohBool.False.ToString());
    }

    [Fact]
    public void Integer_ToString_ReturnsDigits()
    {
        Assert.Equal("42", new ZohInt(42).ToString());
        Assert.Equal("-42", new ZohInt(-42).ToString());
        Assert.Equal("0", new ZohInt(0).ToString());
    }

    [Fact]
    public void Float_ToString_AlwaysIncludesDecimal()
    {
        Assert.Equal("42.0", new ZohFloat(42).ToString());
        Assert.Equal("42.5", new ZohFloat(42.5).ToString());
        Assert.Equal("-42.0", new ZohFloat(-42).ToString());
        Assert.Equal("0.0", new ZohFloat(0).ToString());

        // Spec limits might imply specific precision or scientific notation tests
        Assert.Equal("Infinity", new ZohFloat(double.PositiveInfinity).ToString());
        Assert.Equal("-Infinity", new ZohFloat(double.NegativeInfinity).ToString());
        Assert.Equal("NaN", new ZohFloat(double.NaN).ToString());
    }

    [Fact]
    public void String_ToString_ReturnsIdentity()
    {
        Assert.Equal("hello", new ZohStr("hello").ToString());
    }

    [Fact]
    public void List_ToString_ReturnsFormattedList()
    {
        // [1, "two", 3.0]
        var list = new ZohList(ImmutableArray.Create<ZohValue>(
            new ZohInt(1),
            new ZohStr("two"),
            new ZohFloat(3)
        ));

        // Spec says strings in collections are quoted.
        // List: "[1, "two", 3.0]"
        Assert.Equal("[1, \"two\", 3.0]", list.ToString());
    }

    [Fact]
    public void Map_ToString_ReturnsFormattedMap()
    {
        // {"key": "val", "num": 1}
        // Note: Map order is implementation defined, but dict usually stable for small sets? 
        // We might need looser assertion if order varies.
        var builder = ImmutableDictionary.CreateBuilder<string, ZohValue>();
        builder["key"] = new ZohStr("val");
        builder["num"] = new ZohInt(1);
        var map = new ZohMap(builder.ToImmutable());

        // Keys are quoted. Values are formatted.
        // Spec: "{"k1": v1, ...}"
        // ZohMap backing is Dictionary. Order not guaranteed?
        string s = map.ToString();
        Assert.StartsWith("{", s);
        Assert.EndsWith("}", s);
        Assert.Contains("\"key\": \"val\"", s);
        Assert.Contains("\"num\": 1", s);
    }
    [Fact]
    public void Verb_ToString_ReconstructsCall()
    {
        // /core.verb [attr: "val"] named: 10, "unnamed";
        var call = new Zoh.Runtime.Parsing.Ast.VerbCallAst(
            "core",
            "verb",
            false,
            ImmutableArray.Create(new Zoh.Runtime.Parsing.Ast.AttributeAst("attr", new Zoh.Runtime.Parsing.Ast.ValueAst.String("val"), new Zoh.Runtime.Lexing.TextPosition(0, 0, 0))),
            ImmutableDictionary<string, Zoh.Runtime.Parsing.Ast.ValueAst>.Empty.Add("named", new Zoh.Runtime.Parsing.Ast.ValueAst.Integer(10)),
            ImmutableArray.Create<Zoh.Runtime.Parsing.Ast.ValueAst>(new Zoh.Runtime.Parsing.Ast.ValueAst.String("unnamed")),
            new Zoh.Runtime.Lexing.TextPosition(0, 0, 0)
        );

        var verb = new ZohVerb(call);
        // Expect: /core.verb [attr:"val"] named:10, "unnamed";
        // Note: my ZohVerb implementation puts space before [attr] and params.
        // Let's match implementation:
        // /core.verb [attr:"val"] named:10, "unnamed";
        // Wait, map/dict enumeration order for NamedParams?
        // NamedParams is ImmutableDictionary. Order undefined? Using one param to avoid order issue.

        // My Reconstruct logic:
        // sb.Append(" [").Append(attr.Name).Append(':').Append(val)
        // sb.Append(", ") separated params.

        var s = verb.ToString();
        Assert.StartsWith("/core.verb", s);
        Assert.Contains("[attr:\"val\"]", s); // quoted string value in attribute
        Assert.Contains("named:10", s);
        Assert.Contains("\"unnamed\"", s); // quoted string unnamed param
        Assert.EndsWith(";", s);
    }
}
