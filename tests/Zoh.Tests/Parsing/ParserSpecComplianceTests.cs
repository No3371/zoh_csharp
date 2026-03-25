using Zoh.Runtime.Lexing;
using Zoh.Runtime.Parsing;
using Zoh.Runtime.Parsing.Ast;
using Zoh.Runtime.Types; // For Literal types? No, ValueAst uses primitives or wrapper.
// Actually ValueAst types are in Zoh.Runtime.Parsing.Ast.
using Xunit;
using Xunit.Abstractions; // Added for ITestOutputHelper
using System.Linq; // Added for .Any()

namespace Zoh.Tests.Parsing;

public class ParserSpecComplianceTests
{
    private readonly ITestOutputHelper _output;

    public ParserSpecComplianceTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private ParseResult Parse(string source, bool header)
    {
        var lexer = new Lexer(source, header);
        var lexResult = lexer.Tokenize();
        if (lexResult.Errors.Any())
        {
            foreach (var diag in lexResult.Errors)
            {
                _output.WriteLine($"Lexer Error: {diag.Message} at {diag.Position}");
            }
        }

        var parser = new Parser(lexResult.Tokens);
        var result = parser.Parse();
        if (!result.Success)
        {
            foreach (var error in result.Errors)
            {
                _output.WriteLine($"Parser Error: {error.Message} at {error.Position}");
            }
        }
        return result;
    }

    [Fact]
    public void Spec_StoryStructure_HeaderAndSeparator()
    {
        // Spec Line 300+
        var source = @"My Story
author: ""Author""
version: 1.0
===
@start
";
        var result = Parse(source, true);
        Assert.True(result.Success);
        Assert.Equal("My Story", result.Story!.Name);
        Assert.Contains(result.Story.Metadata, kvp => kvp.Key == "author" && ((ValueAst.String)kvp.Value).Value == "Author");
        // Version might be parsed as double or string? Lexer number.
        // Metadata values are objects?
        // Parser logic for metadata:
        // ParseMetadata: identifier: literal
    }

    [Fact]
    public void Spec_VerbCall_ComplexSignature()
    {
        // Spec Line 120: /verb [attr:val] param1, named:param2;
        var source = "/dialogue [mood: \"happy\"] to: \"player\", \"Hello there!\";";
        var result = Parse(source, false);
        Assert.True(result.Success);

        var call = ((StatementAst.VerbCall)result.Story!.Statements[0]).Call;
        Assert.Equal("dialogue", call.Name);

        // Attribute
        Assert.Single(call.Attributes);
        var attr = call.Attributes[0];
        Assert.Equal("mood", attr.Name);
        Assert.IsType<ValueAst.String>(attr.Value);
        Assert.Equal("happy", ((ValueAst.String)attr.Value).Value);

        // Named Param
        Assert.True(call.NamedParams.ContainsKey("to"));
        Assert.IsType<ValueAst.String>(call.NamedParams["to"]);
        Assert.Equal("player", ((ValueAst.String)call.NamedParams["to"]).Value);

        // Unnamed Param
        Assert.Single(call.UnnamedParams);
        Assert.Equal("Hello there!", ((ValueAst.String)call.UnnamedParams[0]).Value);
    }

    [Fact]
    public void Spec_SetSugar_VarAssignment()
    {
        // Spec: *var <- value;
        var source = "*score <- 100;";
        var result = Parse(source, false);
        Assert.True(result.Success);

        var call = ((StatementAst.VerbCall)result.Story!.Statements[0]).Call;
        Assert.Equal("set", call.Name);
        Assert.Equal("core.var", call.Namespace);
        Assert.Equal(2, call.UnnamedParams.Length);

        // Param 0: Reference OR String? Logic in Parser converts `*name` to Reference or String?
        // Parser.ParseSetSugar: 
        // Consume(Star); Consume(Identifier); -> Name.
        // Returns VerbCall("core", "set", ..., [Ref(name), Value])?
        // Let's check Parser implementation.
        // It produces `new ValueAst.Reference(name)` for first param.

        var p0 = call.UnnamedParams[0];
        Assert.IsType<ValueAst.Reference>(p0);
        Assert.Equal("score", ((ValueAst.Reference)p0).Name);

        var p1 = call.UnnamedParams[1];
        Assert.IsType<ValueAst.Integer>(p1);
        Assert.Equal(100, ((ValueAst.Integer)p1).Value);
    }

    [Fact]
    public void Spec_GetSugar_Retrieval()
    {
        // Spec: <- *var;
        var source = "<- *input;";
        var result = Parse(source, false);
        Assert.True(result.Success);

        var call = ((StatementAst.VerbCall)result.Story!.Statements[0]).Call;
        Assert.Equal("get", call.Name);
        Assert.Equal("core.var", call.Namespace);

        var p0 = call.UnnamedParams[0];
        Assert.IsType<ValueAst.Reference>(p0);
        Assert.Equal("input", ((ValueAst.Reference)p0).Name);
    }

    [Fact]
    public void Spec_InterpolationSugar_String()
    {
        // Spec: /"Hello *name"; -> /interpolate "Hello *name";
        var source = "/\"Hello *name\";";
        var result = Parse(source, false);
        Assert.True(result.Success);

        var call = ((StatementAst.VerbCall)result.Story!.Statements[0]).Call;
        Assert.Equal("interpolate", call.Name);
        Assert.Equal("core.eval", call.Namespace);

        var p0 = call.UnnamedParams[0];
        Assert.IsType<ValueAst.String>(p0);
        Assert.Equal("Hello *name", ((ValueAst.String)p0).Value);
    }

    [Fact]
    public void Spec_EvaluationSugar_Backticks()
    {
        // Spec: /`1 + 1`; -> /evaluate `1 + 1`;
        var source = "/`1 + 1`;";
        var result = Parse(source, false);
        Assert.True(result.Success);

        var call = ((StatementAst.VerbCall)result.Story!.Statements[0]).Call;
        Assert.Equal("evaluate", call.Name);
        Assert.Equal("core.eval", call.Namespace);

        var p0 = call.UnnamedParams[0];
        var expr = Assert.IsType<ValueAst.Expression>(p0);
        Assert.Equal("1 + 1", expr.Source);
    }

    [Fact]
    public void Spec_NestedVerbCalls()
    {
        // Spec: /log /get *x;;
        // "Verbs as values" - assume inner verb ends with ;
        var source = "/log /get *x;;";
        var result = Parse(source, false);
        Assert.True(result.Success);

        var call = ((StatementAst.VerbCall)result.Story!.Statements[0]).Call;
        Assert.Equal("log", call.Name);

        var p0 = call.UnnamedParams[0];
        var verbVal = Assert.IsType<ValueAst.Verb>(p0);
        Assert.Equal("get", verbVal.Call.Name);
        Assert.Equal("x", ((ValueAst.Reference)verbVal.Call.UnnamedParams[0]).Name);
    }

    [Fact]
    public void Spec_BlockVerb_Structure()
    {
        // Spec: /choice/ ... /;
        // Block form content is parsed as parameters (verb values), not statements
        var source = @"/choice/
    -> *option1;
    -> *option2;
/;";
        var result = Parse(source, false);
        Assert.True(result.Success);

        var block = ((StatementAst.VerbCall)result.Story!.Statements[0]).Call;
        Assert.Equal("choice", block.Name);
        Assert.True(block.IsBlock);

        // Block content goes into UnnamedParams as verb values
        Assert.Equal(2, block.UnnamedParams.Length);

        var verb1 = Assert.IsType<ValueAst.Verb>(block.UnnamedParams[0]);
        Assert.Equal("capture", verb1.Call.Name);

        var verb2 = Assert.IsType<ValueAst.Verb>(block.UnnamedParams[1]);
        Assert.Equal("capture", verb2.Call.Name);
    }


}
