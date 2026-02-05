using System.Collections.Immutable;
using Xunit;
using Zoh.Runtime.Execution;
using Zoh.Runtime.Lexing;
using Zoh.Runtime.Parsing.Ast;
using Zoh.Runtime.Types;
using Zoh.Runtime.Variables;
using Zoh.Runtime.Verbs.Core;
using Zoh.Tests.Execution;

namespace Zoh.Tests.Runtime;

public class SetVerbSpecTests
{
    private readonly TestExecutionContext _context;
    private readonly SetDriver _driver;

    public SetVerbSpecTests()
    {
        _context = new TestExecutionContext();
        _driver = new SetDriver();
    }

    private VerbCallAst MakeSetCall(ValueAst target, ValueAst value, params AttributeAst[] attributes)
    {
        return new VerbCallAst(
            "core", "set", false, [.. attributes],
            ImmutableDictionary<string, ValueAst>.Empty,
            [target, value],
            new TextPosition(1, 1, 0));
    }

    private AttributeAst MakeAttr(string name, ValueAst? val = null)
    {
        return new AttributeAst(name, val, new TextPosition(1, 1, 0));
    }

    [Fact]
    public void Set_ResolveAttribute_Resolved()
    {
        // /set [resolve] *var `1+1`
        // Should resolve expression to 2
        var target = new ValueAst.Reference("var");
        var expr = new ValueAst.Expression("1+1", new TextPosition(1, 1, 0));

        var call = MakeSetCall(target, expr, MakeAttr("resolve"));
        var result = _driver.Execute(_context, call);

        Assert.True(result.IsSuccess, "Execution failed");
        var stored = _context.Variables.Get("var");
        Assert.IsType<ZohInt>(stored);
        Assert.Equal(2, ((ZohInt)stored).Value);
    }

    [Fact]
    public void Set_NoResolve_StoresEndpoint()
    {
        // /set *var `1+1`
        // Should store the expression/value AST wrapped in ZohExpr/ZohVerb/etc WITHOUT evaluating
        var target = new ValueAst.Reference("var");
        var exprString = "1+1";
        var expr = new ValueAst.Expression(exprString, new TextPosition(1, 1, 0));

        var call = MakeSetCall(target, expr);
        var result = _driver.Execute(_context, call);

        Assert.True(result.IsSuccess, "Execution failed");
        var stored = _context.Variables.Get("var");
        Assert.IsType<ZohExpr>(stored);
        Assert.Equal(exprString, ((ZohExpr)stored).ast.Source);
    }

    [Fact]
    public void Set_TargetString_Fails()
    {
        // /set "var" 1
        // Should fail because target must be a reference
        var target = new ValueAst.String("var");
        var val = new ValueAst.Integer(1);

        var call = MakeSetCall(target, val);
        var result = _driver.Execute(_context, call);

        Assert.False(result.IsSuccess, "Should fail for String target");
        Assert.Contains(result.Diagnostics, d => d.Code == "invalid_type");
    }

    [Fact]
    public void Set_TargetReference_Succeeds()
    {
        // /set *var 1
        var target = new ValueAst.Reference("var");
        var val = new ValueAst.Integer(1);

        var call = MakeSetCall(target, val);
        var result = _driver.Execute(_context, call);

        Assert.True(result.IsSuccess);
        Assert.Equal(new ZohInt(1), _context.Variables.Get("var"));
    }

    [Fact]
    public void Set_Required_ChecksExistence()
    {
        // Case 1: Variable does not exist, No Value -> Fail
        var target = new ValueAst.Reference("reqVar");
        // Create call with only 1 param (target)
        var callFail = new VerbCallAst(
            "core", "set", false, [MakeAttr("required")],
            ImmutableDictionary<string, ValueAst>.Empty,
            [target],
            new TextPosition(1, 1, 0));

        var resultFail = _driver.Execute(_context, callFail);
        Assert.False(resultFail.IsSuccess, "Should fail when required variable is missing and no value provided");

        // Case 2: Variable exists, No Value -> Succeed
        _context.Variables.Set("reqVar", new ZohInt(1));
        var resultSuccess = _driver.Execute(_context, callFail);
        Assert.True(resultSuccess.IsSuccess, "Should succeed when required variable exists");
    }

    [Fact]
    public void Set_Typed_EnforcesType()
    {
        // /set [typed: "string"] *var 123 -> Fail
        var target = new ValueAst.Reference("typedVar");
        var val = new ValueAst.Integer(123);

        var call = MakeSetCall(target, val, MakeAttr("typed", new ValueAst.String("string")));
        var result = _driver.Execute(_context, call);

        Assert.False(result.IsSuccess, "Should fail type check");
    }
}
