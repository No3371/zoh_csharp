using Zoh.Runtime.Execution;
using Zoh.Runtime.Parsing.Ast;
using Zoh.Runtime.Types;
using Zoh.Runtime.Diagnostics;
using Zoh.Tests.Execution;
using Zoh.Runtime.Verbs.Core;
using System.Collections.Immutable;
using Xunit;

namespace Zoh.Tests.Verbs.Core;

public class DiagnoseTests
{
    private readonly TestExecutionContext _context = new();
    private readonly DiagnoseDriver _driver = new();

    private VerbCallAst MakeCall()
    {
        return new VerbCallAst(
           "core", "diagnose", false, [],
           ImmutableDictionary<string, ValueAst>.Empty,
           [],
           new Zoh.Runtime.Lexing.TextPosition(1, 1, 0));
    }

    [Fact]
    public void Diagnose_ReturnsLastDiagnostics()
    {
        _context.LastDiagnostics.Add(new Diagnostic(DiagnosticSeverity.Warning, "TestWarn", "Warning Msg", new Zoh.Runtime.Lexing.TextPosition(1, 1, 0)));

        var result = _driver.Execute(_context, MakeCall());

        Assert.True(result.IsSuccess);
        Assert.IsType<ZohMap>(result.ValueOrNothing);
        var map = (ZohMap)result.ValueOrNothing;
        Assert.True(map.Items.ContainsKey("warning"));

        var warnings = (ZohList)map.Items["warning"];
        Assert.Equal(new ZohStr("Warning Msg"), warnings.Items[0]);
    }

    [Fact]
    public void Diagnose_Empty_ReturnsNothing()
    {
        _context.LastDiagnostics.Clear();
        var result = _driver.Execute(_context, MakeCall());

        Assert.True(result.IsSuccess);
        Assert.Equal(ZohValue.Nothing, result.ValueOrNothing);
    }
}
