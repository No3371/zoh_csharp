using System.Collections.Generic;
using System.Collections.Immutable;
using Xunit;
using Zoh.Runtime.Execution;
using Zoh.Runtime.Types;
using Zoh.Runtime.Verbs;
using Zoh.Runtime.Verbs.Core;
using Zoh.Runtime.Parsing.Ast;
using Zoh.Runtime.Diagnostics;
using Zoh.Runtime.Lexing;
using Zoh.Tests.Execution;

namespace Zoh.Tests.Verbs.Core
{
    public class TryTests
    {
        private readonly TestExecutionContext _context;

        public TryTests()
        {
            _context = new TestExecutionContext();
            _context.RegisterDriver("try", new TryDriver());

            // Mock verbs
            _context.RegisterDriver("ok", new OkDriver());
            _context.RegisterDriver("fail", new FailDriver());
            _context.RegisterDriver("catch", new CatchDriver());
        }

        [Fact]
        public void Try_ExecutesVerb_ReturnsResult()
        {
            // /try /ok
            var call = CreateVerbCall("try", new ValueAst.Verb(CreateVerbCall("ok")));
            var result = _context.ExecuteVerb(call);

            Assert.True(result.IsSuccess);
            Assert.Equal("ok", result.Value.ToString());
        }

        [Fact]
        public void Try_DowngradesFatal_ToError()
        {
            // /try /fail
            var call = CreateVerbCall("try", new ValueAst.Verb(CreateVerbCall("fail")));
            var result = _context.ExecuteVerb(call);

            Assert.False(result.IsFatal);
            // Result is success with error diagnostics
            Assert.False(result.IsFatal);
            // Result is NOT Success because it has Errors, but it is NOT Fatal.

            Assert.Contains(result.Diagnostics, d => d.Severity == DiagnosticSeverity.Error && d.Code == "fatal_error");
            Assert.DoesNotContain(result.Diagnostics, d => d.Severity == DiagnosticSeverity.Fatal);
            Assert.Equal(ZohValue.Nothing, result.Value);
        }

        [Fact]
        public void Try_ExecutesCatch_OnFatal()
        {
            // /try /fail, catch: /catch
            var call = CreateVerbCall("try", new ValueAst.Verb(CreateVerbCall("fail")));
            call = AddNamedParam(call, "catch", new ValueAst.Verb(CreateVerbCall("catch")));

            var result = _context.ExecuteVerb(call);

            Assert.False(result.IsFatal);
            Assert.Equal("caught", result.Value.ToString());
            Assert.Contains(result.Diagnostics, d => d.Severity == DiagnosticSeverity.Error && d.Code == "fatal_error");
        }

        [Fact]
        public void Try_Suppress_ClearsDiagnostics()
        {
            // /try [suppress] /fail
            var call = CreateVerbCall("try", new ValueAst.Verb(CreateVerbCall("fail")));
            call = AddAttribute(call, "suppress");

            var result = _context.ExecuteVerb(call);

            Assert.False(result.IsFatal);
            Assert.Empty(result.Diagnostics);
        }

        // Helpers
        private VerbCallAst CreateVerbCall(string name, params ValueAst[] unnamedParams)
        {
            return new VerbCallAst(
                null, name, false,
                ImmutableArray<AttributeAst>.Empty,
                ImmutableDictionary<string, ValueAst>.Empty,
                unnamedParams.ToImmutableArray(),
                new TextPosition(0, 0, 0)
            );
        }

        private VerbCallAst AddNamedParam(VerbCallAst call, string name, ValueAst value)
        {
            return call with { NamedParams = call.NamedParams.Add(name, value) };
        }

        private VerbCallAst AddAttribute(VerbCallAst call, string name)
        {
            var attr = new AttributeAst(name, null, new TextPosition(0, 0, 0));
            return call with { Attributes = call.Attributes.Add(attr) };
        }

        // Mock Drivers
        class OkDriver : IVerbDriver
        {
            public string Namespace => "test";
            public string Name => "ok";
            public VerbResult Execute(IExecutionContext context, VerbCallAst call) => VerbResult.Ok(new ZohStr("ok"));
        }

        class FailDriver : IVerbDriver
        {
            public string Namespace => "test";
            public string Name => "fail";
            public VerbResult Execute(IExecutionContext context, VerbCallAst call) =>
                VerbResult.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "fatal_error", "oops", call.Start));
        }

        class CatchDriver : IVerbDriver
        {
            public string Namespace => "test";
            public string Name => "catch";
            public VerbResult Execute(IExecutionContext context, VerbCallAst call) => VerbResult.Ok(new ZohStr("caught"));
        }
    }
}
