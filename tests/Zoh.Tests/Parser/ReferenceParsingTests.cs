using System.Linq;
using Xunit;
using Zoh.Runtime.Lexing;
using Zoh.Runtime.Parsing;
using Zoh.Runtime.Parsing.Ast;
using System.Collections.Immutable;

namespace Zoh.Tests.ParserTests;

public class ReferenceParsingTests
{
    private ValueAst.Reference ParseRef(string input)
    {
        var lexer = new Lexer(input, false);
        var tokens = lexer.Tokenize();
        var parser = new Parser(tokens.Tokens);

        var parseResult = parser.Parse();
        if (parseResult.Errors.Any())
        {
            throw new System.Exception($"Parse error: {parseResult.Errors.First().Message}");
        }

        var stmt = parseResult.Story.Statements[0] as StatementAst.VerbCall;
        return stmt.Call.UnnamedParams[0] as ValueAst.Reference;
    }

    [Fact]
    public void Reference_NoIndex_HasEmptyPath()
    {
        // /set *simple, 1;
        var refVal = ParseRef("/set *simple, 1;");
        Assert.Equal("simple", refVal.Name);
        Assert.Empty(refVal.Path);
    }

    [Fact]
    public void Reference_SingleIndex_ParsesPath()
    {
        // /set *list[0], 1;
        var refVal = ParseRef("/set *list[0], 1;");
        Assert.Equal("list", refVal.Name);
        Assert.Single(refVal.Path);
        var idx = Assert.IsType<ValueAst.Integer>(refVal.Path[0]);
        Assert.Equal(0, idx.Value);
    }

    [Fact]
    public void Reference_MultipleIndexes_ParsesPath()
    {
        // /set *data["users"][0]["name"], "alice";
        var refVal = ParseRef("""/set *data["users"][0]["name"], "alice";""");
        Assert.Equal("data", refVal.Name);
        Assert.Equal(3, refVal.Path.Length);

        Assert.Equal("users", ((ValueAst.String)refVal.Path[0]).Value);
        Assert.Equal(0, ((ValueAst.Integer)refVal.Path[1]).Value);
        Assert.Equal("name", ((ValueAst.String)refVal.Path[2]).Value);
    }
}
