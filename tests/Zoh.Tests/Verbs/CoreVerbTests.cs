using System.Collections.Generic;
using Zoh.Runtime.Execution;
using Zoh.Runtime.Parsing.Ast;
using Zoh.Runtime.Types;
using Zoh.Runtime.Variables;
using Zoh.Runtime.Verbs.Core;
using Zoh.Runtime.Verbs;
using System.Collections.Immutable;
using Xunit;
using Zoh.Tests.Execution;
using Zoh.Runtime.Lexing;
using Zoh.Runtime.Verbs.Flow;

namespace Zoh.Tests.Verbs;

public class CoreVerbTests
{
    private readonly TestExecutionContext _context;

    // Drivers
    private readonly SetDriver _setDriver = new();
    private readonly GetDriver _getDriver = new();
    private readonly DropDriver _dropDriver = new();
    private readonly CaptureDriver _captureDriver = new();
    private readonly TypeDriver _typeDriver = new();
    private readonly IncreaseDriver _increaseDriver = new();
    private readonly DecreaseDriver _decreaseDriver = new();

    public CoreVerbTests()
    {
        _context = new TestExecutionContext();
        _context.RegisterDriver("set", _setDriver);
        _context.RegisterDriver("get", _getDriver);
        _context.RegisterDriver("append", new AppendDriver());
        _context.RegisterDriver("increase", _increaseDriver);
        _context.RegisterDriver("sequence", new SequenceDriver());
    }

    private VerbCallAst MakeVerbCall(string name, params ValueAst[] args)
    {
        return new VerbCallAst(
            "core", name, false, [],
            ImmutableDictionary<string, ValueAst>.Empty,
            [.. args],
            new Zoh.Runtime.Lexing.TextPosition(1, 1, 0));
    }

    private VerbCallAst MakeVerbCallWithAttrs(string name, AttributeAst[] attrs, params ValueAst[] unnamedParams)
    {
        return new VerbCallAst(
            "core", name, false, [.. attrs],
            ImmutableDictionary<string, ValueAst>.Empty,
            [.. unnamedParams],
            new Zoh.Runtime.Lexing.TextPosition(1, 1, 0));
    }

    #region Set Tests

    [Fact]
    public void Set_Basic_SetsVariable()
    {
        // /set *x 10
        var call = MakeVerbCall("set", new ValueAst.Reference("x"), new ValueAst.Integer(10));
        var result = _setDriver.Execute(_context, call);

        Assert.True(result.IsSuccess);
        Assert.Equal(new ZohInt(10), _context.Variables.Get("x"));
    }

    [Fact]
    public void Set_Scope_Context_Shadows()
    {
        // Setup Story scope
        _context.Variables.Set("g", new ZohInt(1), Scope.Story);

        // /set [scope: "context"] *g 2
        var attrs = new[] {
            new AttributeAst("scope", new ValueAst.String("context"), new Zoh.Runtime.Lexing.TextPosition(1,1,0))
        };
        var call = MakeVerbCallWithAttrs("set", attrs, new ValueAst.Reference("g"), new ValueAst.Integer(2));

        _setDriver.Execute(_context, call);

        // Context scope should have 2, but Story shadows it, so Get returns 1
        Assert.Equal(new ZohInt(1), _context.Variables.Get("g"));

        // Drop Story variable to reveal context variable
        _context.Variables.Drop("g");
        Assert.Equal(new ZohInt(2), _context.Variables.Get("g"));
    }

    [Fact]
    public void Set_Typed_CorrectType_Succeeds()
    {
        // /set [typed: "integer"] *x 42
        var attrs = new[] {
            new AttributeAst("typed", new ValueAst.String("integer"), new Zoh.Runtime.Lexing.TextPosition(1,1,0))
        };
        var call = MakeVerbCallWithAttrs("set", attrs, new ValueAst.Reference("x"), new ValueAst.Integer(42));
        var result = _setDriver.Execute(_context, call);

        Assert.True(result.IsSuccess);
        Assert.Equal(new ZohInt(42), _context.Variables.Get("x"));
    }

    [Fact]
    public void Set_Typed_WrongType_Fails()
    {
        // /set [typed: "string"] *x 42  -- should fail
        var attrs = new[] {
            new AttributeAst("typed", new ValueAst.String("string"), new Zoh.Runtime.Lexing.TextPosition(1,1,0))
        };
        var call = MakeVerbCallWithAttrs("set", attrs, new ValueAst.Reference("x"), new ValueAst.Integer(42));
        var result = _setDriver.Execute(_context, call);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void Set_Required_NoValue_Fails()
    {
        // /set [required] *missing  -- no value, no existing
        var attrs = new[] {
            new AttributeAst("required", null, new Zoh.Runtime.Lexing.TextPosition(1,1,0))
        };
        var call = MakeVerbCallWithAttrs("set", attrs, new ValueAst.Reference("missing"));
        var result = _setDriver.Execute(_context, call);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void Set_Required_WithValue_Succeeds()
    {
        // /set [required] *x 10  -- has value, succeeds
        var attrs = new[] {
            new AttributeAst("required", null, new Zoh.Runtime.Lexing.TextPosition(1,1,0))
        };
        var call = MakeVerbCallWithAttrs("set", attrs, new ValueAst.Reference("x"), new ValueAst.Integer(10));
        var result = _setDriver.Execute(_context, call);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Set_OneOf_ValidValue_Succeeds()
    {
        // /set [OneOf: [1,2,3]] *x 2  -- 2 is in list
        var allowedList = new ZohList([new ZohInt(1), new ZohInt(2), new ZohInt(3)]);
        _context.Variables.Set("allowedValues", allowedList);

        var attrs = new[] {
            new AttributeAst("OneOf", new ValueAst.Reference("allowedValues"), new Zoh.Runtime.Lexing.TextPosition(1,1,0))
        };
        var call = MakeVerbCallWithAttrs("set", attrs, new ValueAst.Reference("x"), new ValueAst.Integer(2));
        var result = _setDriver.Execute(_context, call);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Set_OneOf_InvalidValue_Fails()
    {
        // /set [OneOf: [1,2,3]] *x 5  -- 5 is NOT in list
        var allowedList = new ZohList([new ZohInt(1), new ZohInt(2), new ZohInt(3)]);
        _context.Variables.Set("allowedValues", allowedList);

        var attrs = new[] {
            new AttributeAst("OneOf", new ValueAst.Reference("allowedValues"), new Zoh.Runtime.Lexing.TextPosition(1,1,0))
        };
        var call = MakeVerbCallWithAttrs("set", attrs, new ValueAst.Reference("x"), new ValueAst.Integer(5));
        var result = _setDriver.Execute(_context, call);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void Set_Resolve_Expression_StoresEvaluatedValue()
    {
        // /set [resolve] *x `1 + 2`
        var attrs = new[] {
            new AttributeAst("resolve", null, new TextPosition(1,1,0))
        };
        var exprAst = new ValueAst.Expression("1 + 2", new TextPosition(1, 1, 0));
        var call = MakeVerbCallWithAttrs("set", attrs, new ValueAst.Reference("x"), exprAst);

        var result = _setDriver.Execute(_context, call);

        Assert.True(result.IsSuccess);
        Assert.Equal(new ZohInt(3), _context.Variables.Get("x")); // Evaluated instead of ZohExpr
    }

    [Fact]
    public void Set_NoResolve_Expression_StoresZohExpr()
    {
        // /set *x `1 + 2`
        var exprAst = new ValueAst.Expression("1 + 2", new TextPosition(1, 1, 0));
        var call = MakeVerbCall("set", new ValueAst.Reference("x"), exprAst);

        var result = _setDriver.Execute(_context, call);

        Assert.True(result.IsSuccess);
        var val = _context.Variables.Get("x");
        Assert.IsType<ZohExpr>(val);
    }

    #endregion

    #region Get Tests


    [Fact]
    public void Get_Variable_ReturnsValue()
    {
        _context.Variables.Set("myVar", new ZohStr("TestValue"));

        // /get *myVar
        var call = MakeVerbCall("get", new ValueAst.Reference("myVar"));
        var result = _getDriver.Execute(_context, call);

        Assert.True(result.IsSuccess);
        Assert.Equal(new ZohStr("TestValue"), result.Value);
    }

    [Fact]
    public void Get_MissingVariable_ReturnsNothing()
    {
        var call = MakeVerbCall("get", new ValueAst.Reference("missing"));
        var result = _getDriver.Execute(_context, call);

        Assert.True(result.IsSuccess);
        Assert.Equal(ZohValue.Nothing, result.Value); // VariableStore.Get returns Nothing if missing
    }

    #endregion

    #region Capture Tests

    [Fact]
    public void Capture_LastResult_SetsVariable()
    {
        // Simulate previous result
        _context.LastResult = new ZohInt(42);

        // /capture *res
        var call = MakeVerbCall("capture", new ValueAst.Reference("res"));
        var result = _captureDriver.Execute(_context, call);

        Assert.True(result.IsSuccess);
        Assert.Equal(new ZohInt(42), _context.Variables.Get("res"));
        Assert.Equal(new ZohInt(42), result.Value);
    }

    #endregion

    #region Drop Tests

    [Fact]
    public void Drop_ExistingVariable_RemovesIt()
    {
        _context.Variables.Set("rem", new ZohInt(1));
        Assert.Equal(new ZohInt(1), _context.Variables.Get("rem"));

        // /drop *rem
        var call = MakeVerbCall("drop", new ValueAst.Reference("rem"));
        var result = _dropDriver.Execute(_context, call);

        Assert.True(result.IsSuccess);
        Assert.Equal(ZohValue.Nothing, _context.Variables.Get("rem"));
    }

    #endregion

    #region Type Tests

    [Fact]
    public void Type_ReturnsCorrectString()
    {
        // /type 123
        var call = MakeVerbCall("type", new ValueAst.Integer(123));
        var result = _typeDriver.Execute(_context, call);

        Assert.True(result.IsSuccess);
        Assert.Equal(new ZohStr("integer"), result.Value);
    }



    public static IEnumerable<object[]> GetTypeTestCases()
    {
        yield return new object[] { new ValueAst.Integer(1), "integer" };
        yield return new object[] { new ValueAst.Double(1.5), "double" };
        yield return new object[] { new ValueAst.Boolean(true), "boolean" };
        yield return new object[] { new ValueAst.String("s"), "string" };
        yield return new object[] { new ValueAst.Nothing(), "nothing" };
        yield return new object[] { new ValueAst.Channel("c"), "channel" };
    }

    [Fact]
    public void Type_ReturnsExpression_ForZohExpr()
    {
        _context.Variables.Set("exprVar", new ZohExpr(new ValueAst.Expression("1+1", new TextPosition(0, 0, 0))));
        var call = MakeVerbCall("type", new ValueAst.Reference("exprVar"));
        var result = _typeDriver.Execute(_context, call);

        Assert.True(result.IsSuccess);
        Assert.Equal(new ZohStr("expression"), result.Value);
    }

    [Theory]
    [MemberData(nameof(GetSimpleTypeTestCases))]
    public void Type_ReturnsSpecCompliantStrings_ForLiterals(ValueAst ast, string expected)
    {
        var call = MakeVerbCall("type", ast);
        var result = _typeDriver.Execute(_context, call);
        Assert.True(result.IsSuccess);
        Assert.Equal(new ZohStr(expected), result.Value);
    }

    public static IEnumerable<object[]> GetSimpleTypeTestCases()
    {
        yield return new object[] { new ValueAst.Integer(42), "integer" };
        yield return new object[] { new ValueAst.Double(3.14), "double" };
        yield return new object[] { new ValueAst.Boolean(true), "boolean" };
        yield return new object[] { new ValueAst.String("hello"), "string" };
        yield return new object[] { new ValueAst.Nothing(), "nothing" };
        yield return new object[] { new ValueAst.Channel("any"), "channel" };
    }

    [Fact]
    public void Type_ReturnsList_ForList()
    {
        var listAst = new ValueAst.List(System.Collections.Immutable.ImmutableArray<ValueAst>.Empty);
        var call = MakeVerbCall("type", listAst);
        var result = _typeDriver.Execute(_context, call);
        Assert.Equal(new ZohStr("list"), result.Value);
    }

    [Fact]
    public void Type_ReturnsMap_ForMap()
    {
        var mapAst = new ValueAst.Map(System.Collections.Immutable.ImmutableArray<(ValueAst, ValueAst)>.Empty);
        var call = MakeVerbCall("type", mapAst);
        var result = _typeDriver.Execute(_context, call);
        Assert.Equal(new ZohStr("map"), result.Value);
    }

    #endregion

    #region Increase/Decrease Tests

    [Fact]
    public void Increase_WithInvalidTypeAmount_Fails()
    {
        _context.Variables.Set("cnt", new ZohInt(5));

        // /increase *cnt "string"
        var call = MakeVerbCall("increase", new ValueAst.Reference("cnt"), new ValueAst.String("string"));
        var result = _increaseDriver.Execute(_context, call);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, d => d.Code == "invalid_type");
    }

    [Fact]
    public void Increase_WithVerbAmount_ExecutesVerb()
    {
        _context.Variables.Set("cnt", new ZohInt(5));

        // /increase *cnt, /rand 1, 10;; -> /rand returns 7 for instance, but we use an easier test verb like /get
        _context.Variables.Set("amt", new ZohInt(3));
        var getCall = MakeVerbCall("get", new ValueAst.Reference("amt"));

        var call = MakeVerbCall("increase", new ValueAst.Reference("cnt"), new ValueAst.Verb(getCall));
        var result = _increaseDriver.Execute(_context, call);

        Assert.True(result.IsSuccess);
        Assert.Equal(new ZohInt(8), _context.Variables.Get("cnt"));
    }

    [Fact]
    public void Increase_Int_Increments()
    {
        _context.Variables.Set("cnt", new ZohInt(5));

        // /increase *cnt
        var call = MakeVerbCall("increase", new ValueAst.Reference("cnt"));
        var result = _increaseDriver.Execute(_context, call);

        Assert.True(result.IsSuccess);
        Assert.Equal(new ZohInt(6), _context.Variables.Get("cnt"));
    }

    [Fact]
    public void Increase_IntRef_Increments()
    {
        _context.Variables.Set("cntRef", new ZohInt(5));

        // /increase *cntRef
        var call = MakeVerbCall("increase", new ValueAst.Reference("cntRef"));
        var result = _increaseDriver.Execute(_context, call);

        Assert.True(result.IsSuccess);
        Assert.Equal(new ZohInt(6), _context.Variables.Get("cntRef"));
    }

    [Fact]
    public void Increase_Float_Increments()
    {
        _context.Variables.Set("f", new ZohFloat(5.5));

        // /increase *f
        var call = MakeVerbCall("increase", new ValueAst.Reference("f"));
        var result = _increaseDriver.Execute(_context, call);

        Assert.True(result.IsSuccess);
        Assert.Equal(new ZohFloat(6.5), _context.Variables.Get("f"));
    }

    [Fact]
    public void Increase_IntByFloat_PromotesToFloat()
    {
        _context.Variables.Set("n", new ZohInt(10));

        // /increase *n 0.5
        var call = MakeVerbCall("increase", new ValueAst.Reference("n"), new ValueAst.Double(0.5));
        var result = _increaseDriver.Execute(_context, call);

        Assert.True(result.IsSuccess);
        Assert.IsType<ZohFloat>(_context.Variables.Get("n"));
        Assert.Equal(new ZohFloat(10.5), _context.Variables.Get("n"));
    }

    [Fact]
    public void Decrease_Basic_Decrements()
    {
        _context.Variables.Set("d", new ZohInt(10));

        // /decrease *d 2
        var call = MakeVerbCall("decrease", new ValueAst.Reference("d"), new ValueAst.Integer(2));
        var result = _decreaseDriver.Execute(_context, call);

        Assert.True(result.IsSuccess);
        Assert.Equal(new ZohInt(8), _context.Variables.Get("d"));
    }

    #endregion

    #region Has Tests

    [Fact]
    public void Has_ListContainsItem_ReturnsTrue()
    {
        _context.Variables.Set("list", new ZohList([new ZohInt(1), new ZohInt(2), new ZohInt(3)]));

        // /has *list 2
        var call = MakeVerbCall("has", new ValueAst.Reference("list"), new ValueAst.Integer(2));
        var result = new HasDriver().Execute(_context, call);

        Assert.True(result.IsSuccess);
        Assert.Equal(ZohValue.True, result.Value);
    }

    [Fact]
    public void Has_ListMissingItem_ReturnsFalse()
    {
        _context.Variables.Set("list", new ZohList([new ZohInt(1), new ZohInt(2)]));

        // /has *list 5
        var call = MakeVerbCall("has", new ValueAst.Reference("list"), new ValueAst.Integer(5));
        var result = new HasDriver().Execute(_context, call);

        Assert.True(result.IsSuccess);
        Assert.Equal(ZohValue.False, result.Value);
    }

    [Fact]
    public void Has_MapContainsKey_ReturnsTrue()
    {
        var map = new ZohMap(ImmutableDictionary<string, ZohValue>.Empty.Add("key1", new ZohInt(1)));
        _context.Variables.Set("map", map);

        // /has *map "key1"
        var call = MakeVerbCall("has", new ValueAst.Reference("map"), new ValueAst.String("key1"));
        var result = new HasDriver().Execute(_context, call);

        Assert.True(result.IsSuccess);
        Assert.Equal(ZohValue.True, result.Value);
    }

    #endregion

    #region Any Tests

    [Fact]
    public void Any_WithValue_ReturnsTrue()
    {
        _context.Variables.Set("x", new ZohInt(5));

        // /any *x
        var call = MakeVerbCall("any", new ValueAst.Reference("x"));
        var result = new AnyDriver().Execute(_context, call);

        Assert.True(result.IsSuccess);
        Assert.Equal(ZohValue.True, result.Value);
    }

    [Fact]
    public void Any_WithNothing_ReturnsFalse()
    {
        // *missing doesn't exist -> Nothing
        var call = MakeVerbCall("any", new ValueAst.Reference("missing"));
        var result = new AnyDriver().Execute(_context, call);

        Assert.True(result.IsSuccess);
        Assert.Equal(ZohValue.False, result.Value);
    }

    #endregion

    #region First Tests

    [Fact]
    public void First_MultipleSources_ReturnsFirstNonNothing()
    {
        _context.Variables.Set("b", new ZohStr("second"));

        // /first *missing *b
        var call = MakeVerbCall("first", new ValueAst.Reference("missing"), new ValueAst.Reference("b"));
        var result = new FirstDriver().Execute(_context, call);

        Assert.True(result.IsSuccess);
        Assert.Equal(new ZohStr("second"), result.Value);
    }

    [Fact]
    public void First_EvaluatesVerbsAndExpressionsDynamically()
    {
        // /first `1 + 2`
        var exprAst = new ValueAst.Expression("1 + 2", new TextPosition(1, 1, 0));

        // /get *x  where *x = 42
        _context.Variables.Set("x", new ZohInt(42));
        var getCall = MakeVerbCall("get", new ValueAst.Reference("x"));
        var verbAst = new ValueAst.Verb(getCall);

        var firstCall1 = MakeVerbCall("first", new ValueAst.Nothing(), exprAst);
        var res1 = new FirstDriver().Execute(_context, firstCall1);

        var firstCall2 = MakeVerbCall("first", new ValueAst.Nothing(), verbAst);
        var res2 = new FirstDriver().Execute(_context, firstCall2);

        Assert.True(res1.IsSuccess);
        Assert.Equal(new ZohInt(3), res1.Value);

        Assert.True(res2.IsSuccess);
        Assert.Equal(new ZohInt(42), res2.Value);
    }

    #endregion

    #region Append Tests

    [Fact]
    public void Append_AddToList()
    {
        _context.Variables.Set("arr", new ZohList([new ZohInt(1)]));

        // /append *arr 2
        var call = MakeVerbCall("append", new ValueAst.Reference("arr"), new ValueAst.Integer(2));
        var result = new AppendDriver().Execute(_context, call);

        Assert.True(result.IsSuccess);
        var list = _context.Variables.Get("arr") as ZohList;
        Assert.NotNull(list);
        Assert.Equal(2, list.Items.Length);
        Assert.Equal(new ZohInt(2), list.Items[1]);
    }

    #endregion

    #region Roll Tests

    [Fact]
    public void Roll_ReturnsOneOfOptions()
    {
        // /roll 10, 20, 30
        var call = MakeVerbCall("roll", new ValueAst.Integer(10), new ValueAst.Integer(20), new ValueAst.Integer(30));
        var result = new RollDriver().Execute(_context, call);

        Assert.True(result.IsSuccess);
        var val = ((ZohInt)result.Value).Value;
        Assert.Contains(val, new long[] { 10, 20, 30 });
    }

    [Fact]
    public void Rand_ReturnsInRange()
    {
        // /rand 1, 10
        var call = MakeVerbCall("rand", new ValueAst.Integer(1), new ValueAst.Integer(10));
        // Manually set verb name to "rand"
        var randCall = new VerbCallAst(
            "core", "rand", false, [],
            ImmutableDictionary<string, ValueAst>.Empty,
            [new ValueAst.Integer(1), new ValueAst.Integer(10)],
            new Zoh.Runtime.Lexing.TextPosition(1, 1, 0));

        var result = new RollDriver().Execute(_context, randCall);

        Assert.True(result.IsSuccess);
        var val = ((ZohInt)result.Value).Value;
        Assert.InRange(val, 1, 9); // exclusive max by default
    }

    #endregion

    #region Parse Tests

    [Fact]
    public void Parse_Integer()
    {
        // /parse "42"
        var call = MakeVerbCall("parse", new ValueAst.String("42"));
        var result = new ParseDriver().Execute(_context, call);

        Assert.True(result.IsSuccess);
        Assert.Equal(new ZohInt(42), result.Value);
    }

    [Fact]
    public void Parse_Double()
    {
        // /parse "3.14", "double"
        var call = MakeVerbCall("parse", new ValueAst.String("3.14"), new ValueAst.String("double"));
        var result = new ParseDriver().Execute(_context, call);

        Assert.True(result.IsSuccess);
        Assert.Equal(new ZohFloat(3.14), result.Value);
    }

    [Fact]
    public void Parse_Boolean()
    {
        // /parse "true", "boolean"
        var call = MakeVerbCall("parse", new ValueAst.String("true"), new ValueAst.String("boolean"));
        var result = new ParseDriver().Execute(_context, call);

        Assert.True(result.IsSuccess);
        Assert.Equal(ZohValue.True, result.Value);
    }

    [Fact]
    public void Parse_Invalid_ThrowsError()
    {
        // /parse "abc", "integer"
        var call = MakeVerbCall("parse", new ValueAst.String("abc"), new ValueAst.String("integer"));
        var result = new ParseDriver().Execute(_context, call);

        Assert.False(result.IsSuccess);
    }

    #endregion

    #region Debug Tests

    [Fact]
    public void Info_ReturnsInfoDiagnostic()
    {
        // /info "test message"
        var call = new VerbCallAst(
            "core", "info", false, [],
            ImmutableDictionary<string, ValueAst>.Empty,
            [new ValueAst.String("test message")],
            new Zoh.Runtime.Lexing.TextPosition(1, 1, 0));

        var result = new DebugDriver().Execute(_context, call);

        Assert.True(result.IsSuccess);
        Assert.NotEmpty(result.Diagnostics);
        Assert.Equal(Zoh.Runtime.Diagnostics.DiagnosticSeverity.Info, result.Diagnostics[0].Severity);
    }

    [Fact]
    public void Error_ReturnsFailure()
    {
        // /error "error message"
        var call = new VerbCallAst(
            "core", "error", false, [],
            ImmutableDictionary<string, ValueAst>.Empty,
            [new ValueAst.String("error message")],
            new Zoh.Runtime.Lexing.TextPosition(1, 1, 0));

        var result = new DebugDriver().Execute(_context, call);

        Assert.False(result.IsSuccess); // Error level = failure
    }

    #endregion
    #region Sequence Tests

    [Fact]
    public void Sequence_ExecutesVerbsInOrder()
    {
        _context.Variables.Set("log", new ZohList([]));

        // /sequence /append *log "a";, /append *log "b";, /append *log "c";;
        var append1 = MakeVerbCall("append", new ValueAst.Reference("log"), new ValueAst.String("a"));
        var append2 = MakeVerbCall("append", new ValueAst.Reference("log"), new ValueAst.String("b"));
        var append3 = MakeVerbCall("append", new ValueAst.Reference("log"), new ValueAst.String("c"));

        var call = MakeVerbCall("sequence",
            new ValueAst.Verb(append1),
            new ValueAst.Verb(append2),
            new ValueAst.Verb(append3));

        new SequenceDriver().Execute(_context, call);

        var log = (ZohList)_context.Variables.Get("log");
        Assert.Equal(3, log.Items.Length);
        Assert.Equal(new ZohStr("a"), log.Items[0]);
        Assert.Equal(new ZohStr("b"), log.Items[1]);
        Assert.Equal(new ZohStr("c"), log.Items[2]);
    }

    [Fact]
    public void Sequence_ReturnsLastVerbResult()
    {
        // /sequence /set *x, 1;, /set *y, 2;, /get *y;;
        var set1 = MakeVerbCall("set", new ValueAst.Reference("x"), new ValueAst.Integer(1));
        var set2 = MakeVerbCall("set", new ValueAst.Reference("y"), new ValueAst.Integer(2));
        var get = MakeVerbCall("get", new ValueAst.Reference("y"));

        var call = MakeVerbCall("sequence",
            new ValueAst.Verb(set1),
            new ValueAst.Verb(set2),
            new ValueAst.Verb(get));

        var result = new SequenceDriver().Execute(_context, call);

        Assert.Equal(new ZohInt(2), result.Value);
    }

    [Fact]
    public void Sequence_WithBreakIf_StopsEarly()
    {
        _context.Variables.Set("cnt", new ZohInt(0));

        // /increase *cnt;
        var inc = MakeVerbCall("increase", new ValueAst.Reference("cnt"));

        // breakif: `*cnt >= 2`
        var breakExpr = new ValueAst.Expression("*cnt >= 2", new TextPosition(0, 0, 0));
        var namedParams = ImmutableDictionary<string, ValueAst>.Empty.Add("breakif", breakExpr);

        var call = new VerbCallAst(
            "core", "sequence", false, [],
            namedParams,
            [new ValueAst.Verb(inc), new ValueAst.Verb(inc), new ValueAst.Verb(inc)],
            new TextPosition(1, 1, 0));

        new SequenceDriver().Execute(_context, call);

        Assert.Equal(new ZohInt(2), _context.Variables.Get("cnt"));
    }

    [Fact]
    public void Sequence_Empty_ReturnsNothing()
    {
        var call = MakeVerbCall("sequence");
        var result = new SequenceDriver().Execute(_context, call);

        Assert.Equal(ZohValue.Nothing, result.Value);
    }

    #endregion
}
