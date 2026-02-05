using System.Collections.Generic;
using Xunit;
using Zoh.Runtime.Execution;
using Zoh.Runtime.Types;
using Zoh.Tests.Execution;
using Zoh.Runtime.Parsing.Ast;

namespace Zoh.Tests.Runtime;

public class NestedAccessTests
{
    private TestExecutionContext _context = new();
    private Dictionary<string, Zoh.Runtime.Verbs.IVerbDriver> _drivers = new()
    {
        { "set", new Zoh.Runtime.Verbs.Core.SetDriver() },
        { "get", new Zoh.Runtime.Verbs.Core.GetDriver() },
        { "drop", new Zoh.Runtime.Verbs.Core.DropDriver() },
        { "capture", new Zoh.Runtime.Verbs.Core.CaptureDriver() },
        { "increase", new Zoh.Runtime.Verbs.Core.IncreaseDriver() }
    };

    private void Execute(string script)
    {
        var lexer = new Zoh.Runtime.Lexing.Lexer(script);
        var tokens = lexer.Tokenize();
        // LexResult has Tokens property
        var parser = new Zoh.Runtime.Parsing.Parser(tokens.Tokens);
        var result = parser.Parse();

        if (result.Story == null) return;

        foreach (var stmt in result.Story.Statements)
        {
            if (stmt is StatementAst.VerbCall call)
            {
                if (_drivers.TryGetValue(call.Call.Name, out var driver))
                {
                    var vResult = driver.Execute(_context, call.Call);
                    // Set LastResult for chain
                    _context.LastResult = vResult.Value ?? ZohValue.Nothing;
                }
                else
                {
                    // implicit pass?
                }
            }
        }
    }

    [Fact]
    public void Set_NestedMapValue_Works()
    {
        // Must setup structure first? 
        // *data <- {"user": {"name": "Bob"}}
        // Note: Parser supports map literals? parser.cs showed ParseMap.
        // Nested map *creation* via Set logic.

        Execute(@"
            *data <- {""user"": {""name"": ""Bob""}};
            *data[""user""][""name""] <- ""Alice"";
        ");
        var data = _context.Variables.Get("data") as ZohMap;
        Assert.NotNull(data);
        var user = data.Items["user"] as ZohMap;
        Assert.NotNull(user);
        Assert.Equal("Alice", ((ZohStr)user.Items["name"]).Value);
    }

    [Fact]
    public void Get_NestedListValue_Works()
    {
        // List literal logic support? Yes parser supports it.
        Execute(@"
            *data <- [[1, 2], [3, 4]];
            /get *data[1][0]; -> *result;
        ");
        // note: -> *result calls capture.

        var res = _context.Variables.Get("result");
        Assert.Equal(3, ((ZohInt)res).Value);
    }

    [Fact]
    public void Set_NewKey_In_NestedMap_Works()
    {
        Execute(@"
            *data <- {""user"": {}};
            *data[""user""][""age""] <- 30;
         ");
        var data = _context.Variables.Get("data") as ZohMap;
        var user = data.Items["user"] as ZohMap;
        Assert.Equal(30, ((ZohInt)user.Items["age"]).Value);
    }

    [Fact]
    public void Set_IntermediateMissing_Fatals()
    {
        // This won't throw exception, but Execute will continue.
        // We verify data is NOT changed or created.

        Execute(@"
            *data <- {};
            *data[""missing""][""key""] <- ""value"";
         ");

        var data = _context.Variables.Get("data") as ZohMap;
        Assert.False(data.Items.ContainsKey("missing"));
    }

    [Fact]
    public void Drop_NestedElement_SetsToNothing()
    {
        Execute(@"
            *data <- {""key"": ""value""};
            /drop *data[""key""];
         ");

        var data = _context.Variables.Get("data") as ZohMap;
        Assert.True(data.Items.ContainsKey("key")); // Key remains but value is Nothing?
                                                    // Spec: "Dropping element: Set to nothing (map) or Nothing (list?)"
                                                    // DropDriver -> SetAtPath(Nothing).
                                                    // Map SetItem(key, Nothing) -> Key remains, value is Nothing.

        Assert.IsType<ZohNothing>(data.Items["key"]);
    }

    [Fact]
    public void Increase_NestedValue_Works()
    {
        Execute(@"
            *scores <- [10, 20];
            /increase *scores[1], 5;
         ");

        var scores = _context.Variables.Get("scores") as ZohList;
        Assert.Equal(25, ((ZohInt)scores.Items[1]).Value);
    }
}
