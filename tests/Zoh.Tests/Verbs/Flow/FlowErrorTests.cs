using System.Collections.Generic;
using System.Collections.Immutable;
using Xunit;
using Zoh.Runtime.Execution;
using Zoh.Runtime.Types;
using Zoh.Runtime.Verbs;
using Zoh.Runtime.Verbs.Flow;
using Zoh.Runtime.Parsing.Ast;
using Zoh.Runtime.Lexing;
using Zoh.Runtime.Diagnostics;
using Zoh.Tests.Execution;

namespace Zoh.Tests.Verbs.Flow
{
    public class FlowErrorTests
    {
        private readonly TestExecutionContext _context;

        public FlowErrorTests()
        {
            _context = new TestExecutionContext();
            _context.RegisterDriver("if", new IfDriver());
            _context.RegisterDriver("loop", new LoopDriver());
            _context.RegisterDriver("while", new WhileDriver());
            _context.RegisterDriver("sequence", new SequenceDriver());
            _context.RegisterDriver("foreach", new ForeachDriver());

            _context.RegisterDriver("fatal_verb", new FatalDriver());
            _context.RegisterDriver("record", new RecordDriver());
            _context.Variables.Set("record_count", new ZohInt(0));
        }

        private VerbCallAst CreateVerbCall(string name, params ValueAst[] unnamedParams) =>
            new VerbCallAst(null, name, false, ImmutableArray<AttributeAst>.Empty, ImmutableDictionary<string, ValueAst>.Empty, unnamedParams.ToImmutableArray(), new TextPosition(0, 0, 0));

        private VerbCallAst AddNamedParam(VerbCallAst call, string name, ValueAst value) =>
            call with { NamedParams = call.NamedParams.Add(name, value) };

        // --- Loop Tests ---
        [Fact]
        public void Loop_MissingParameters_ReturnsFatal()
        {
            var call = CreateVerbCall("loop");
            var result = _context.ExecuteVerb(call);
            Assert.True(result.IsFatal);
            Assert.Contains(result.DiagnosticsOrEmpty, d => d.Code == "parameter_not_found");
        }

        [Fact]
        public void Loop_InvalidIterationsType_ReturnsFatal()
        {
            var call = CreateVerbCall("loop", new ValueAst.String("5"), new ValueAst.Verb(CreateVerbCall("record")));
            var result = _context.ExecuteVerb(call);
            Assert.True(result.IsFatal);
            Assert.Contains(result.DiagnosticsOrEmpty, d => d.Code == "invalid_type");
        }

        [Fact]
        public void Loop_InvalidVerbType_ReturnsFatal()
        {
            var call = CreateVerbCall("loop", new ValueAst.Integer(5), new ValueAst.Integer(10));
            var result = _context.ExecuteVerb(call);
            Assert.True(result.IsFatal);
            Assert.Contains(result.DiagnosticsOrEmpty, d => d.Code == "invalid_type");
        }

        [Fact]
        public void Loop_ZeroIterations_DoesNotExecuteBody()
        {
            var call = CreateVerbCall("loop", new ValueAst.Integer(0), new ValueAst.Verb(CreateVerbCall("record")));
            var result = _context.ExecuteVerb(call);
            Assert.True(result.IsSuccess);
            Assert.Equal(0L, _context.Variables.Get("record_count").AsInt().Value);
        }

        [Fact]
        public void Loop_NegativeIterations_DoesNotExecuteBody()
        {
            var call = CreateVerbCall("loop", new ValueAst.Integer(-2), new ValueAst.Verb(CreateVerbCall("record")));
            var result = _context.ExecuteVerb(call);
            Assert.True(result.IsSuccess);
            Assert.Equal(0L, _context.Variables.Get("record_count").AsInt().Value);
        }

        [Fact]
        public void Loop_InfiniteIterations_ExecutesUntilBreak()
        {
            _context.RegisterDriver("check_break", new CheckBreakDriver());
            var seq = CreateVerbCall("sequence", new ValueAst.Verb(CreateVerbCall("record")), new ValueAst.Verb(CreateVerbCall("check_break")));
            var call = CreateVerbCall("loop", new ValueAst.Integer(-1), new ValueAst.Verb(seq));
            call = AddNamedParam(call, "breakif", new ValueAst.Reference("stop"));
            _context.Variables.Set("stop", new ZohBool(false));

            var result = _context.ExecuteVerb(call);
            Assert.True(result.IsSuccess);
            Assert.Equal(3L, _context.Variables.Get("record_count").AsInt().Value);
        }

        [Fact]
        public void Loop_BodyReturnsFatal_HaltsLoopAndReturnsFatal()
        {
            var seq = CreateVerbCall("sequence", new ValueAst.Verb(CreateVerbCall("record")), new ValueAst.Verb(CreateVerbCall("fatal_verb")));
            var call = CreateVerbCall("loop", new ValueAst.Integer(5), new ValueAst.Verb(seq));
            var result = _context.ExecuteVerb(call);
            Assert.True(result.IsFatal);
            Assert.Equal(1L, _context.Variables.Get("record_count").AsInt().Value);
        }

        // --- If Tests ---
        [Fact]
        public void If_MissingCondition_ReturnsFatal()
        {
            var call = CreateVerbCall("if");
            var result = _context.ExecuteVerb(call);
            Assert.True(result.IsFatal);
            Assert.Contains(result.DiagnosticsOrEmpty, d => d.Code == "parameter_not_found");
        }

        [Fact]
        public void If_MissingThenVerb_ReturnsFatal()
        {
            var call = CreateVerbCall("if", new ValueAst.Boolean(true));
            var result = _context.ExecuteVerb(call);
            Assert.True(result.IsFatal);
            Assert.Contains(result.DiagnosticsOrEmpty, d => d.Code == "parameter_not_found");
        }

        [Fact]
        public void If_ConditionNotBooleanAndDefaultCompare_ReturnsFatal()
        {
            var call = CreateVerbCall("if", new ValueAst.Integer(1), new ValueAst.Verb(CreateVerbCall("record")));
            var result = _context.ExecuteVerb(call);
            Assert.True(result.IsFatal);
            Assert.Contains(result.DiagnosticsOrEmpty, d => d.Code == "invalid_type");
        }

        [Fact]
        public void If_InvalidThenType_ReturnsFatal()
        {
            var call = CreateVerbCall("if", new ValueAst.Boolean(true), new ValueAst.String("not a verb"));
            var result = _context.ExecuteVerb(call);
            Assert.True(result.IsFatal);
            Assert.Contains(result.DiagnosticsOrEmpty, d => d.Code == "invalid_type");
        }

        [Fact]
        public void If_ConditionFalseAndNoElse_ReturnsOk()
        {
            var call = CreateVerbCall("if", new ValueAst.Boolean(false), new ValueAst.Verb(CreateVerbCall("record")));
            var result = _context.ExecuteVerb(call);
            Assert.True(result.IsSuccess);
            Assert.Equal(0L, _context.Variables.Get("record_count").AsInt().Value);
        }

        [Fact]
        public void If_InvalidElseType_ReturnsFatal()
        {
            var call = CreateVerbCall("if", new ValueAst.Boolean(false), new ValueAst.Verb(CreateVerbCall("record")), new ValueAst.String("invalid"));
            var result = _context.ExecuteVerb(call);
            Assert.True(result.IsFatal);
            Assert.Contains(result.DiagnosticsOrEmpty, d => d.Code == "invalid_type");
        }

        [Fact]
        public void If_BodyReturnsFatal_PropagatesFatal()
        {
            var call = CreateVerbCall("if", new ValueAst.Boolean(true), new ValueAst.Verb(CreateVerbCall("fatal_verb")));
            var result = _context.ExecuteVerb(call);
            Assert.True(result.IsFatal);
        }

        // --- While Tests ---
        [Fact]
        public void While_MissingParameters_ReturnsFatal()
        {
            var call = CreateVerbCall("while", new ValueAst.Boolean(true));
            var result = _context.ExecuteVerb(call);
            Assert.True(result.IsFatal);
            Assert.Contains(result.DiagnosticsOrEmpty, d => d.Code == "parameter_not_found");
        }

        [Fact]
        public void While_InvalidVerbType_ReturnsFatal()
        {
            var call = CreateVerbCall("while", new ValueAst.Boolean(true), new ValueAst.String("invalid"));
            var result = _context.ExecuteVerb(call);
            Assert.True(result.IsFatal);
            Assert.Contains(result.DiagnosticsOrEmpty, d => d.Code == "invalid_type");
        }

        [Fact]
        public void While_ConditionNotBooleanAndDefaultCompare_ReturnsFatal()
        {
            var call = CreateVerbCall("while", new ValueAst.Integer(1), new ValueAst.Verb(CreateVerbCall("record")));
            var result = _context.ExecuteVerb(call);
            Assert.True(result.IsFatal);
            Assert.Contains(result.DiagnosticsOrEmpty, d => d.Code == "invalid_type");
        }

        [Fact]
        public void While_ConditionInitiallyFalse_DoesNotExecuteBody()
        {
            var call = CreateVerbCall("while", new ValueAst.Reference("cond"), new ValueAst.Verb(CreateVerbCall("record")));
            _context.Variables.Set("cond", new ZohBool(false));
            var result = _context.ExecuteVerb(call);
            Assert.True(result.IsSuccess);
            Assert.Equal(0L, _context.Variables.Get("record_count").AsInt().Value);
        }

        [Fact]
        public void While_BodyReturnsFatal_HaltsAndPropagates()
        {
            var call = CreateVerbCall("while", new ValueAst.Reference("cond"), new ValueAst.Verb(CreateVerbCall("fatal_verb")));
            _context.Variables.Set("cond", new ZohBool(true));
            var result = _context.ExecuteVerb(call);
            Assert.True(result.IsFatal);
        }

        // --- Foreach Tests ---
        [Fact]
        public void Foreach_NonReferenceIterator_ReturnsFatal()
        {
            var list = new ZohList(ImmutableArray.Create<ZohValue>(new ZohInt(1)));
            _context.Variables.Set("mylist", list);
            var call = CreateVerbCall("foreach",
                new ValueAst.Reference("mylist"),
                new ValueAst.String("item"),
                new ValueAst.Verb(CreateVerbCall("record")));
            var result = _context.ExecuteVerb(call);
            Assert.True(result.IsFatal);
            Assert.Contains(result.DiagnosticsOrEmpty, d => d.Code == "invalid_type");
        }

        // --- Sequence Tests ---
        [Fact]
        public void Sequence_EmptyArguments_ReturnsOk()
        {
            var call = CreateVerbCall("sequence");
            var result = _context.ExecuteVerb(call);
            Assert.True(result.IsSuccess);
        }

        [Fact]
        public void Sequence_InvalidArgumentType_ReturnsFatal()
        {
            var call = CreateVerbCall("sequence", new ValueAst.Verb(CreateVerbCall("record")), new ValueAst.String("invalid"), new ValueAst.Verb(CreateVerbCall("record")));
            var result = _context.ExecuteVerb(call);
            Assert.True(result.IsFatal);
            Assert.Contains(result.DiagnosticsOrEmpty, d => d.Code == "invalid_type");
            Assert.Equal(1L, _context.Variables.Get("record_count").AsInt().Value);
        }

        [Fact]
        public void Sequence_VerbReturnsFatal_HaltsSequence()
        {
            var call = CreateVerbCall("sequence", new ValueAst.Verb(CreateVerbCall("record")), new ValueAst.Verb(CreateVerbCall("fatal_verb")), new ValueAst.Verb(CreateVerbCall("record")));
            var result = _context.ExecuteVerb(call);
            Assert.True(result.IsFatal);
            Assert.Equal(1L, _context.Variables.Get("record_count").AsInt().Value);
        }

        // --- Mock Drivers ---
        class FatalDriver : IVerbDriver
        {
            public string Namespace => "test";
            public string Name => "fatal_verb";
            public DriverResult Execute(IExecutionContext context, VerbCallAst call) => DriverResult.Complete.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "test_error", "triggered test error", call.Start));
        }

        class RecordDriver : IVerbDriver
        {
            public string Namespace => "test";
            public string Name => "record";
            public DriverResult Execute(IExecutionContext context, VerbCallAst call)
            {
                var count = context.Variables.Get("record_count").AsInt().Value;
                context.Variables.Set("record_count", new ZohInt(count + 1));
                return DriverResult.Complete.Ok();
            }
        }
        
        class CheckBreakDriver : IVerbDriver
        {
            public string Namespace => "test";
            public string Name => "check_break";
            public DriverResult Execute(IExecutionContext context, VerbCallAst call)
            {
                var count = context.Variables.Get("record_count").AsInt().Value;
                if (count >= 3) context.Variables.Set("stop", new ZohBool(true));
                return DriverResult.Complete.Ok();
            }
        }
    }
}
