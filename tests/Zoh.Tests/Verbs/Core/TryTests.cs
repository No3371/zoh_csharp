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
            _context.RegisterDriver("suspend_then_ok", new SuspendThenOkDriver());
            _context.RegisterDriver("suspend_then_fail", new SuspendThenFailDriver());
        }

        [Fact]
        public void Try_ExecutesVerb_ReturnsResult()
        {
            // /try /ok
            var call = CreateVerbCall("try", new ValueAst.Verb(CreateVerbCall("ok")));
            var result = _context.ExecuteVerb(call);

            Assert.True(result.IsSuccess);
            Assert.Equal("ok", result.ValueOrNothing.ToString());
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

            Assert.Contains(result.DiagnosticsOrEmpty, d => d.Severity == DiagnosticSeverity.Error && d.Code == "fatal_error");
            Assert.DoesNotContain(result.DiagnosticsOrEmpty, d => d.Severity == DiagnosticSeverity.Fatal);
            Assert.Equal(ZohValue.Nothing, result.ValueOrNothing);
        }

        [Fact]
        public void Try_ExecutesCatch_OnFatal()
        {
            // /try /fail, catch: /catch
            var call = CreateVerbCall("try", new ValueAst.Verb(CreateVerbCall("fail")));
            call = AddNamedParam(call, "catch", new ValueAst.Verb(CreateVerbCall("catch")));

            var result = _context.ExecuteVerb(call);

            Assert.False(result.IsFatal);
            Assert.Equal("caught", result.ValueOrNothing.ToString());
            Assert.Contains(result.DiagnosticsOrEmpty, d => d.Severity == DiagnosticSeverity.Error && d.Code == "fatal_error");
        }

        [Fact]
        public void Try_Suppress_ClearsDiagnostics()
        {
            // /try [suppress] /fail
            var call = CreateVerbCall("try", new ValueAst.Verb(CreateVerbCall("fail")));
            call = AddAttribute(call, "suppress");

            var result = _context.ExecuteVerb(call);

            Assert.False(result.IsFatal);
            Assert.Empty(result.DiagnosticsOrEmpty);
        }

        // --- Suspension wrapping tests ---

        [Fact]
        public void Try_AroundSuspendingVerb_ReturnsSuspend()
        {
            // /try /suspend_then_ok — must return Suspend (not Complete)
            var call = CreateVerbCall("try", new ValueAst.Verb(CreateVerbCall("suspend_then_ok")));
            var result = _context.ExecuteVerb(call);

            Assert.IsType<DriverResult.Suspend>(result);
        }

        [Fact]
        public void Try_AroundSuspendingVerb_WrapsContination_PreservesSameRequest()
        {
            // The wrapped suspension must preserve the original WaitRequest
            var call = CreateVerbCall("try", new ValueAst.Verb(CreateVerbCall("suspend_then_ok")));
            var result = _context.ExecuteVerb(call);

            var suspend = Assert.IsType<DriverResult.Suspend>(result);
            Assert.IsType<SleepRequest>(suspend.Continuation.Request);
        }

        [Fact]
        public void Try_SuspendingVerb_WhenResumedWithOk_ReturnsSuccess()
        {
            // /try /suspend_then_ok — resume → ok result passes through
            var call = CreateVerbCall("try", new ValueAst.Verb(CreateVerbCall("suspend_then_ok")));
            var suspend = Assert.IsType<DriverResult.Suspend>(_context.ExecuteVerb(call));

            var resumed = suspend.Continuation.OnFulfilled(new WaitCompleted(ZohValue.Nothing));

            Assert.True(resumed.IsSuccess);
            Assert.Equal("ok_after_suspend", resumed.ValueOrNothing.ToString());
        }

        [Fact]
        public void Try_SuspendingVerb_WhenResumedWithFatal_DowngradesToError()
        {
            // /try /suspend_then_fail — resume → fatal is downgraded to error
            var call = CreateVerbCall("try", new ValueAst.Verb(CreateVerbCall("suspend_then_fail")));
            var suspend = Assert.IsType<DriverResult.Suspend>(_context.ExecuteVerb(call));

            var resumed = suspend.Continuation.OnFulfilled(new WaitCompleted(ZohValue.Nothing));

            Assert.False(resumed.IsFatal);
            Assert.Contains(resumed.DiagnosticsOrEmpty, d => d.Severity == DiagnosticSeverity.Error && d.Code == "fatal_after_suspend");
            Assert.DoesNotContain(resumed.DiagnosticsOrEmpty, d => d.Severity == DiagnosticSeverity.Fatal);
        }

        [Fact]
        public void Try_SuspendingVerb_WithCatch_WhenResumedWithFatal_ExecutesCatch()
        {
            // /try /suspend_then_fail, catch: /catch — catch runs after resume fatal
            var call = CreateVerbCall("try", new ValueAst.Verb(CreateVerbCall("suspend_then_fail")));
            call = AddNamedParam(call, "catch", new ValueAst.Verb(CreateVerbCall("catch")));
            var suspend = Assert.IsType<DriverResult.Suspend>(_context.ExecuteVerb(call));

            var resumed = suspend.Continuation.OnFulfilled(new WaitCompleted(ZohValue.Nothing));

            Assert.False(resumed.IsFatal);
            Assert.Equal("caught", resumed.ValueOrNothing.ToString());
        }

        [Fact]
        public void Try_SuspendingVerb_WithSuppress_WhenResumedWithFatal_ClearsDiagnostics()
        {
            // /try [suppress] /suspend_then_fail — diagnostics cleared after resume
            var call = CreateVerbCall("try", new ValueAst.Verb(CreateVerbCall("suspend_then_fail")));
            call = AddAttribute(call, "suppress");
            var suspend = Assert.IsType<DriverResult.Suspend>(_context.ExecuteVerb(call));

            var resumed = suspend.Continuation.OnFulfilled(new WaitCompleted(ZohValue.Nothing));

            Assert.False(resumed.IsFatal);
            Assert.Empty(resumed.DiagnosticsOrEmpty);
        }

        [Fact]
        public void Try_ChainedSuspension_BothWrapped()
        {
            // Inner verb suspends twice — second suspend is also wrapped by try logic
            _context.RegisterDriver("suspend_chain", new SuspendChainDriver());
            var call = CreateVerbCall("try", new ValueAst.Verb(CreateVerbCall("suspend_chain")));

            // First resume returns another Suspend
            var suspend1 = Assert.IsType<DriverResult.Suspend>(_context.ExecuteVerb(call));
            var suspend2 = Assert.IsType<DriverResult.Suspend>(
                suspend1.Continuation.OnFulfilled(new WaitCompleted(ZohValue.Nothing)));

            // Second resume produces a fatal — must be downgraded by try
            var final = suspend2.Continuation.OnFulfilled(new WaitCompleted(ZohValue.Nothing));
            Assert.False(final.IsFatal);
            Assert.Contains(final.DiagnosticsOrEmpty, d => d.Severity == DiagnosticSeverity.Error);
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
            public DriverResult Execute(IExecutionContext context, VerbCallAst call) => DriverResult.Complete.Ok(new ZohStr("ok"));
        }

        class FailDriver : IVerbDriver
        {
            public string Namespace => "test";
            public string Name => "fail";
            public DriverResult Execute(IExecutionContext context, VerbCallAst call) =>
                DriverResult.Complete.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "fatal_error", "oops", call.Start));
        }

        class CatchDriver : IVerbDriver
        {
            public string Namespace => "test";
            public string Name => "catch";
            public DriverResult Execute(IExecutionContext context, VerbCallAst call) => DriverResult.Complete.Ok(new ZohStr("caught"));
        }

        /// <summary>Suspends once, then returns an ok value on resume.</summary>
        class SuspendThenOkDriver : IVerbDriver
        {
            public string Namespace => "test";
            public string Name => "suspend_then_ok";
            public DriverResult Execute(IExecutionContext context, VerbCallAst call) =>
                new DriverResult.Suspend(new Continuation(
                    new SleepRequest(100),
                    _ => DriverResult.Complete.Ok(new ZohStr("ok_after_suspend"))
                ));
        }

        /// <summary>Suspends once, then produces a fatal on resume.</summary>
        class SuspendThenFailDriver : IVerbDriver
        {
            public string Namespace => "test";
            public string Name => "suspend_then_fail";
            public DriverResult Execute(IExecutionContext context, VerbCallAst call) =>
                new DriverResult.Suspend(new Continuation(
                    new SleepRequest(100),
                    _ => DriverResult.Complete.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "fatal_after_suspend", "fatal after resume", new TextPosition(0, 0, 0)))
                ));
        }

        /// <summary>Suspends twice, then produces a fatal on second resume.</summary>
        class SuspendChainDriver : IVerbDriver
        {
            public string Namespace => "test";
            public string Name => "suspend_chain";
            public DriverResult Execute(IExecutionContext context, VerbCallAst call) =>
                new DriverResult.Suspend(new Continuation(
                    new SleepRequest(100),
                    _ => new DriverResult.Suspend(new Continuation(
                        new SleepRequest(200),
                        __ => DriverResult.Complete.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "fatal_in_chain", "fatal in second resume", new TextPosition(0, 0, 0)))
                    ))
                ));
        }
    }
}
