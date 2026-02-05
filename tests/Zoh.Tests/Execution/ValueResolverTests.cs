using System.Collections.Immutable;
using Zoh.Runtime.Execution;
using Zoh.Runtime.Lexing;
using Zoh.Runtime.Parsing.Ast;
using Zoh.Runtime.Types;
using Zoh.Runtime.Variables;

namespace Zoh.Tests.Execution;

public class ValueResolverTests
{
    private readonly TestExecutionContext _context;

    public ValueResolverTests()
    {
        _context = new TestExecutionContext();
    }

    #region Literal Resolution Tests

    [Fact]
    public void Resolve_Nothing_ReturnsNothing()
    {
        var ast = new ValueAst.Nothing();
        var result = ValueResolver.Resolve(ast, _context);

        Assert.Equal(ZohValue.Nothing, result);
    }

    [Fact]
    public void Resolve_True_ReturnsTrue()
    {
        var ast = new ValueAst.Boolean(true);
        var result = ValueResolver.Resolve(ast, _context);

        Assert.Equal(ZohValue.True, result);
    }

    [Fact]
    public void Resolve_False_ReturnsFalse()
    {
        var ast = new ValueAst.Boolean(false);
        var result = ValueResolver.Resolve(ast, _context);

        Assert.Equal(ZohValue.False, result);
    }

    [Fact]
    public void Resolve_Integer_ReturnsZohInt()
    {
        var ast = new ValueAst.Integer(42);
        var result = ValueResolver.Resolve(ast, _context);

        Assert.IsType<ZohInt>(result);
        Assert.Equal(new ZohInt(42), result);
    }

    [Fact]
    public void Resolve_Double_ReturnsZohFloat()
    {
        var ast = new ValueAst.Double(3.14);
        var result = ValueResolver.Resolve(ast, _context);

        Assert.IsType<ZohFloat>(result);
        Assert.Equal(new ZohFloat(3.14), result);
    }

    [Fact]
    public void Resolve_String_ReturnsZohStr()
    {
        var ast = new ValueAst.String("Hello");
        var result = ValueResolver.Resolve(ast, _context);

        Assert.IsType<ZohStr>(result);
        Assert.Equal(new ZohStr("Hello"), result);
    }

    [Fact]
    public void Resolve_Channel_ReturnsZohChannel()
    {
        var ast = new ValueAst.Channel("data");
        var result = ValueResolver.Resolve(ast, _context);

        Assert.IsType<ZohChannel>(result);
        var chan = (ZohChannel)result;
        Assert.Equal("data", chan.Name);
    }

    #endregion

    #region Reference Resolution Tests

    [Fact]
    public void Resolve_Reference_ReturnsVariableValue()
    {
        _context.Variables.Set("name", new ZohStr("Alice"));
        var ast = new ValueAst.Reference("name", null);
        var result = ValueResolver.Resolve(ast, _context);

        Assert.Equal(new ZohStr("Alice"), result);
    }

    [Fact]
    public void Resolve_Reference_UndefinedVariable_ReturnsNothing()
    {
        var ast = new ValueAst.Reference("undefined", null);
        var result = ValueResolver.Resolve(ast, _context);

        Assert.Equal(ZohValue.Nothing, result);
    }

    [Fact]
    public void Resolve_IndexedReference_List_ReturnsElement()
    {
        var list = new ZohList([new ZohInt(10), new ZohInt(20), new ZohInt(30)]);
        _context.Variables.Set("arr", list);

        var indexAst = new ValueAst.Integer(1);
        var ast = new ValueAst.Reference("arr", indexAst);
        var result = ValueResolver.Resolve(ast, _context);

        Assert.Equal(new ZohInt(20), result);
    }

    [Fact]
    public void Resolve_IndexedReference_List_OutOfBounds_ReturnsNothing()
    {
        var list = new ZohList([new ZohInt(10), new ZohInt(20)]);
        _context.Variables.Set("arr", list);

        var indexAst = new ValueAst.Integer(5);
        var ast = new ValueAst.Reference("arr", indexAst);
        var result = ValueResolver.Resolve(ast, _context);

        Assert.Equal(ZohValue.Nothing, result);
    }

    [Fact]
    public void Resolve_IndexedReference_List_NegativeIndex_ReturnsElement()
    {
        var list = new ZohList([new ZohInt(10), new ZohInt(20), new ZohInt(30)]);
        _context.Variables.Set("arr", list);

        // Index -1 refers to the last element (30)
        var indexAst = new ValueAst.Integer(-1);
        var ast = new ValueAst.Reference("arr", indexAst);
        var result = ValueResolver.Resolve(ast, _context);

        Assert.Equal(new ZohInt(30), result);
    }

    [Fact]
    public void Resolve_IndexedReference_Map_ReturnsValue()
    {
        var map = new ZohMap(new Dictionary<string, ZohValue>
        {
            ["key1"] = new ZohInt(100),
            ["key2"] = new ZohStr("value")
        }.ToImmutableDictionary());
        _context.Variables.Set("obj", map);

        var indexAst = new ValueAst.String("key1");
        var ast = new ValueAst.Reference("obj", indexAst);
        var result = ValueResolver.Resolve(ast, _context);

        Assert.Equal(new ZohInt(100), result);
    }

    [Fact]
    public void Resolve_IndexedReference_Map_MissingKey_ReturnsNothing()
    {
        var map = new ZohMap(new Dictionary<string, ZohValue>
        {
            ["key1"] = new ZohInt(100)
        }.ToImmutableDictionary());
        _context.Variables.Set("obj", map);

        var indexAst = new ValueAst.String("missing");
        var ast = new ValueAst.Reference("obj", indexAst);
        var result = ValueResolver.Resolve(ast, _context);

        Assert.Equal(ZohValue.Nothing, result);
    }

    #endregion

    #region Collection Resolution Tests

    [Fact]
    public void Resolve_List_ResolvesElements()
    {
        var ast = new ValueAst.List(ImmutableArray.Create<ValueAst>(
            new ValueAst.Integer(1),
            new ValueAst.Integer(2),
            new ValueAst.Integer(3)
        ));
        var result = ValueResolver.Resolve(ast, _context);

        Assert.IsType<ZohList>(result);
        var list = (ZohList)result;
        Assert.Equal(3, list.Items.Length);
        Assert.Equal(new ZohInt(1), list.Items[0]);
        Assert.Equal(new ZohInt(2), list.Items[1]);
        Assert.Equal(new ZohInt(3), list.Items[2]);
    }

    [Fact]
    public void Resolve_List_WithReferences_ResolvesVariables()
    {
        _context.Variables.Set("x", new ZohInt(10));
        _context.Variables.Set("y", new ZohInt(20));

        var ast = new ValueAst.List(ImmutableArray.Create<ValueAst>(
            new ValueAst.Reference("x", null),
            new ValueAst.Reference("y", null)
        ));
        var result = ValueResolver.Resolve(ast, _context);

        var list = (ZohList)result;
        Assert.Equal(new ZohInt(10), list.Items[0]);
        Assert.Equal(new ZohInt(20), list.Items[1]);
    }

    [Fact]
    public void Resolve_Map_ResolvesEntries()
    {
        var ast = new ValueAst.Map(ImmutableArray.Create<(ValueAst, ValueAst)>(
            (new ValueAst.String("a"), new ValueAst.Integer(1)),
            (new ValueAst.String("b"), new ValueAst.Integer(2))
        ));
        var result = ValueResolver.Resolve(ast, _context);

        Assert.IsType<ZohMap>(result);
        var map = (ZohMap)result;
        Assert.Equal(2, map.Items.Count);
        Assert.Equal(new ZohInt(1), map.Items["a"]);
        Assert.Equal(new ZohInt(2), map.Items["b"]);
    }

    [Fact]
    public void Resolve_Map_WithReferences_ResolvesVariables()
    {
        _context.Variables.Set("val1", new ZohStr("Hello"));
        _context.Variables.Set("val2", new ZohStr("World"));

        var ast = new ValueAst.Map(ImmutableArray.Create<(ValueAst, ValueAst)>(
            (new ValueAst.String("first"), new ValueAst.Reference("val1", null)),
            (new ValueAst.String("second"), new ValueAst.Reference("val2", null))
        ));
        var result = ValueResolver.Resolve(ast, _context);

        var map = (ZohMap)result;
        Assert.Equal(new ZohStr("Hello"), map.Items["first"]);
        Assert.Equal(new ZohStr("World"), map.Items["second"]);
    }

    [Fact]
    public void Resolve_EmptyList_ReturnsEmptyList()
    {
        var ast = new ValueAst.List(ImmutableArray<ValueAst>.Empty);
        var result = ValueResolver.Resolve(ast, _context);

        var list = (ZohList)result;
        Assert.Empty(list.Items);
    }

    [Fact]
    public void Resolve_EmptyMap_ReturnsEmptyMap()
    {
        var ast = new ValueAst.Map(ImmutableArray<(ValueAst, ValueAst)>.Empty);
        var result = ValueResolver.Resolve(ast, _context);

        var map = (ZohMap)result;
        Assert.Empty(map.Items);
    }

    #endregion

    #region Expression Resolution Tests

    [Fact]
    public void Resolve_Expression_SimpleArithmetic_Evaluates()
    {
        var ast = new ValueAst.Expression("2 + 3", new TextPosition(1, 1, 0));
        var result = ValueResolver.Resolve(ast, _context);

        Assert.Equal(new ZohInt(5), result);
    }

    [Fact]
    public void Resolve_Expression_WithVariable_Evaluates()
    {
        _context.Variables.Set("x", new ZohInt(10));

        var ast = new ValueAst.Expression("*x * 2", new TextPosition(1, 1, 0));
        var result = ValueResolver.Resolve(ast, _context);

        Assert.Equal(new ZohInt(20), result);
    }

    [Fact]
    public void Resolve_Expression_Comparison_Evaluates()
    {
        _context.Variables.Set("age", new ZohInt(25));

        var ast = new ValueAst.Expression("*age >= 18", new TextPosition(1, 1, 0));
        var result = ValueResolver.Resolve(ast, _context);

        Assert.Equal(ZohValue.True, result);
    }

    #endregion

    #region Nested Resolution Tests

    [Fact]
    public void Resolve_NestedList_ResolvesRecursively()
    {
        var ast = new ValueAst.List(ImmutableArray.Create<ValueAst>(
            new ValueAst.Integer(1),
            new ValueAst.List(ImmutableArray.Create<ValueAst>(
                new ValueAst.Integer(2),
                new ValueAst.Integer(3)
            ))
        ));
        var result = ValueResolver.Resolve(ast, _context);

        var list = (ZohList)result;
        Assert.Equal(2, list.Items.Length);
        Assert.Equal(new ZohInt(1), list.Items[0]);

        var innerList = Assert.IsType<ZohList>(list.Items[1]);
        Assert.Equal(2, innerList.Items.Length);
        Assert.Equal(new ZohInt(2), innerList.Items[0]);
        Assert.Equal(new ZohInt(3), innerList.Items[1]);
    }

    [Fact]
    public void Resolve_IndexedReference_WithExpression_EvaluatesIndex()
    {
        var list = new ZohList([new ZohInt(10), new ZohInt(20), new ZohInt(30)]);
        _context.Variables.Set("arr", list);
        _context.Variables.Set("idx", new ZohInt(2));

        var indexAst = new ValueAst.Expression("*idx - 1", new TextPosition(1, 1, 0));
        var ast = new ValueAst.Reference("arr", indexAst);
        var result = ValueResolver.Resolve(ast, _context);

        Assert.Equal(new ZohInt(20), result); // arr[2-1] = arr[1] = 20
    }

    [Fact]
    public void Resolve_IndexedReference_WithRecursiveExpression_EvaluatesIndex()
    {
        // Feature: Implicit resolution of Expression values as indices
        // *arr[*logic] where *logic -> `1+1` -> 2, so *arr[2]

        var list = new ZohList([new ZohInt(10), new ZohInt(20), new ZohInt(30)]);
        _context.Variables.Set("arr", list);

        // Define *logic as an expression `1 + 1`
        var exprAst = new ValueAst.Expression("1 + 1", new TextPosition(0, 0, 0));
        var logicExpr = new ZohExpr(exprAst);
        _context.Variables.Set("logic", logicExpr);

        // Reference: *arr[*logic]
        var indexAst = new ValueAst.Reference("logic", null);
        var ast = new ValueAst.Reference("arr", indexAst);

        var result = ValueResolver.Resolve(ast, _context);

        Assert.Equal(new ZohInt(30), result); // arr[2] = 30
    }

    [Fact]
    public void Resolve_IndexedReference_InfiniteRecursion_Throws()
    {
        // Feature: Recursive resolution limit
        // *loop [*loop] where *loop -> `*loop`

        var exprAst = new ValueAst.Expression("*loop", new TextPosition(0, 0, 0));
        var loopExpr = new ZohExpr(exprAst);
        _context.Variables.Set("loop", loopExpr);

        var indexAst = new ValueAst.Reference("loop", null);
        var ast = new ValueAst.Reference("loop", indexAst);

        // Should throw ZohDiagnosticException from CollectionHelpers
        var ex = Assert.Throws<ZohDiagnosticException>(() => ValueResolver.Resolve(ast, _context));
        Assert.Equal("runtime_error", ex.DiagnosticCode);
        Assert.Contains("Maximum recursion depth exceeded", ex.Message);
    }

    [Fact]
    public void Resolve_IndexedReference_ExpressionResolvesToInvalidType_Throws()
    {
        // Feature: Implicit resolution with type validation
        // *arr[*bad] where *bad -> `1.5` (double)
        // List requires Int. Type mismatch throws ZohDiagnosticException.

        var list = new ZohList([new ZohInt(10)]);
        _context.Variables.Set("arr", list);

        var exprAst = new ValueAst.Expression("1.5", new TextPosition(0, 0, 0));
        var badExpr = new ZohExpr(exprAst);
        _context.Variables.Set("bad", badExpr);

        var indexAst = new ValueAst.Reference("bad", null);
        var ast = new ValueAst.Reference("arr", indexAst);

        // Helper throws ZohDiagnosticException
        var ex = Assert.Throws<ZohDiagnosticException>(() => ValueResolver.Resolve(ast, _context));
        Assert.Equal("invalid_index_type", ex.DiagnosticCode);
        Assert.Contains("List index must be integer", ex.Message);
    }

    #endregion
}
