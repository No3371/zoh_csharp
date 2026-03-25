using Zoh.Runtime.Parsing.Ast;
using Zoh.Runtime.Types;
using Zoh.Tests.Execution;
using Zoh.Runtime.Verbs.Debug;
using System.Collections.Immutable;
using Xunit;
using Zoh.Runtime.Diagnostics;
using System.Collections.Generic;

namespace Zoh.Tests.Verbs.Core;

public class AssertDriverTests
{
    private readonly TestExecutionContext _context = new();
    private readonly AssertDriver _driver = new();

    private VerbCallAst MakeAssertCall(ValueAst subject, string? isParamName = null, ValueAst? isValue = null, ValueAst? message = null)
    {
        var unnamedParams = new List<ValueAst> { subject };
        if (message != null) unnamedParams.Add(message);

        var namedParams = ImmutableDictionary<string, ValueAst>.Empty;
        if (isParamName != null && isValue != null)
        {
            namedParams = namedParams.Add(isParamName, isValue);
        }

        return new VerbCallAst(
           "core.debug", "assert", false, [],
           namedParams,
           unnamedParams.ToImmutableArray(),
           new Zoh.Runtime.Lexing.TextPosition(1, 1, 0));
    }

    [Fact]
    public void Assert_Truthy_Passes()
    {
        var call = MakeAssertCall(new ValueAst.Boolean(true));
        var result = _driver.Execute(_context, call);
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Assert_Falsy_FailsWithFatal()
    {
        var call = MakeAssertCall(new ValueAst.Boolean(false));
        var result = _driver.Execute(_context, call);
        Assert.False(result.IsSuccess);
        Assert.True(result.IsFatal);
        Assert.Equal("assertion_failed", result.DiagnosticsOrEmpty[0].Code);
    }

    [Fact]
    public void Assert_Truthy_WithIsParameter_Passes()
    {
        // /assert "combat", is: "combat"
        var call = MakeAssertCall(new ValueAst.String("combat"), "is", new ValueAst.String("combat"));
        var result = _driver.Execute(_context, call);
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Assert_Mismatch_WithIsParameter_Fails()
    {
        var call = MakeAssertCall(new ValueAst.String("stealth"), "is", new ValueAst.String("combat"));
        var result = _driver.Execute(_context, call);
        Assert.False(result.IsSuccess);
        Assert.True(result.IsFatal);
        Assert.Equal("assertion_failed", result.DiagnosticsOrEmpty[0].Code);
        Assert.Equal("assertion failed", result.DiagnosticsOrEmpty[0].Message);
    }

    [Fact]
    public void Assert_CustomMessage_IsIncludedInDiagnostic()
    {
        var call = MakeAssertCall(new ValueAst.Boolean(false), message: new ValueAst.String("custom failure message"));
        var result = _driver.Execute(_context, call);
        Assert.False(result.IsSuccess);
        Assert.True(result.IsFatal);
        Assert.Equal("custom failure message", result.DiagnosticsOrEmpty[0].Message);
    }

    [Fact]
    public void Assert_CustomMessage_IsInterpolated()
    {
        _context.Variables.Set("name", new ZohStr("Bob"));
        var call = MakeAssertCall(new ValueAst.Boolean(false), message: new ValueAst.String("hello ${*name}"));
        var result = _driver.Execute(_context, call);
        Assert.False(result.IsSuccess);
        Assert.Equal("hello Bob", result.DiagnosticsOrEmpty[0].Message);
    }

    [Fact]
    public void Assert_NonBooleanWithoutIs_FailsWithInvalidType()
    {
        var call = MakeAssertCall(new ValueAst.String("truthy_string_but_not_bool"));
        var result = _driver.Execute(_context, call);
        Assert.False(result.IsSuccess);
        Assert.True(result.IsFatal);
        Assert.Equal("invalid_type", result.DiagnosticsOrEmpty[0].Code);
    }
}
