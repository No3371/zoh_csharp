using System.Collections.Generic;
using System.Collections.Immutable;
using Xunit;
using Zoh.Runtime.Execution;
using Zoh.Runtime.Types;
using Zoh.Runtime.Verbs;
using Zoh.Runtime.Verbs.Flow;
using Zoh.Runtime.Verbs.Math;
using Zoh.Runtime.Verbs.Var;
using Zoh.Runtime.Parsing.Ast;
using Zoh.Runtime.Lexing;
using Zoh.Runtime.Variables;
using Zoh.Runtime.Diagnostics;
using Zoh.Tests.Execution;

namespace Zoh.Tests.Verbs.Flow
{
    public class FlowTests
    {
        private readonly TestExecutionContext _context;

        public FlowTests()
        {
            _context = new TestExecutionContext();
            // Register flow drivers
            _context.RegisterDriver("if", new IfDriver());
            _context.RegisterDriver("switch", new SwitchDriver());
            _context.RegisterDriver("loop", new LoopDriver());
            _context.RegisterDriver("while", new WhileDriver());
            _context.RegisterDriver("foreach", new ForeachDriver());
            _context.RegisterDriver("sequence", new SequenceDriver());

            // Register helper drivers for side effects
            _context.RegisterDriver("increase", new IncreaseDriver());
            _context.RegisterDriver("set", new SetDriver());
        }

        [Fact]
        public void If_ExecutesThen_WhenConditionTrue()
        {
            _context.Variables.Set("x", new ZohInt(0));
            // /if true, /set *x, 1;

            var thenVerb = CreateVerbCall("set", new ValueAst.Reference("x"), new ValueAst.Integer(1));
            var call = CreateVerbCall("if",
                new ValueAst.Boolean(true),
                new ValueAst.Verb(thenVerb)
            );

            var result = _context.ExecuteVerb(call);
            Assert.True(result.IsSuccess, string.Join(", ", result.DiagnosticsOrEmpty));

            Assert.Equal(1L, _context.Variables.Get("x").AsInt().Value);
        }

        [Fact]
        public void If_ExecutesElse_WhenConditionFalse()
        {
            _context.Variables.Set("x", new ZohInt(0));
            // /if false, /set x, 1;, /set x, 5;;

            var thenVerb = CreateVerbCall("set", new ValueAst.Reference("x"), new ValueAst.Integer(1));
            var elseVerb = CreateVerbCall("set", new ValueAst.Reference("x"), new ValueAst.Integer(5));

            var call = CreateVerbCall("if",
                new ValueAst.Boolean(false),
                new ValueAst.Verb(thenVerb),
                new ValueAst.Verb(elseVerb)
            );

            var result = _context.ExecuteVerb(call);
            Assert.True(result.IsSuccess, string.Join(", ", result.DiagnosticsOrEmpty));

            Assert.Equal(5L, _context.Variables.Get("x").AsInt().Value);
        }

        [Fact]
        public void If_VerbSubjectRunsBeforeThenBranch()
        {
            _context.RegisterDriver("if_order_subject", new IfOrderSubjectDriver());
            _context.RegisterDriver("if_order_then", new IfOrderThenDriver());
            _context.Variables.Set("order", new ZohInt(0));
            _context.Variables.Set("x", new ZohInt(0));

            var subjectVerb = CreateVerbCall("if_order_subject");
            var thenVerb = CreateVerbCall("if_order_then");
            var call = CreateVerbCall("if",
                new ValueAst.Verb(subjectVerb),
                new ValueAst.Verb(thenVerb));

            var result = _context.ExecuteVerb(call);
            Assert.True(result.IsSuccess, string.Join(", ", result.DiagnosticsOrEmpty));
            Assert.Equal(1L, _context.Variables.Get("x").AsInt().Value);
        }

        [Fact]
        public void If_UsesNamedElse()
        {
            _context.Variables.Set("x", new ZohInt(0));
            var thenVerb = CreateVerbCall("set", new ValueAst.Reference("x"), new ValueAst.Integer(1));
            var elseVerb = CreateVerbCall("set", new ValueAst.Reference("x"), new ValueAst.Integer(5));
            var call = CreateVerbCall("if",
                new ValueAst.Boolean(false),
                new ValueAst.Verb(thenVerb));
            call = AddNamedParam(call, "else", new ValueAst.Verb(elseVerb));

            var result = _context.ExecuteVerb(call);
            Assert.True(result.IsSuccess, string.Join(", ", result.DiagnosticsOrEmpty));
            Assert.Equal(5L, _context.Variables.Get("x").AsInt().Value);
        }

        [Fact]
        public void If_DefaultComparison_InvalidTypeAfterSubjectEval()
        {
            _context.RegisterDriver("return_int42", new ReturnInt42Driver());
            var subjectVerb = CreateVerbCall("return_int42");
            var thenVerb = CreateVerbCall("set", new ValueAst.Reference("x"), new ValueAst.Integer(1));
            _context.Variables.Set("x", new ZohInt(0));

            var call = CreateVerbCall("if",
                new ValueAst.Verb(subjectVerb),
                new ValueAst.Verb(thenVerb));

            var result = _context.ExecuteVerb(call);
            Assert.True(result.IsFatal);
            Assert.Contains(result.DiagnosticsOrEmpty, d => d.Code == "invalid_type");
        }

        [Fact]
        public void Switch_ExecutesMatch()
        {
            _context.Variables.Set("val", new ZohStr("b"));
            _context.Variables.Set("res", new ZohInt(0));

            // /switch val, "a", /set res, 1;, "b";, /set res, 2;, "c";, /set res, 3;;
            var call = CreateVerbCall("switch",
                new ValueAst.Reference("val"),
                new ValueAst.String("a"), new ValueAst.Verb(CreateVerbCall("set", new ValueAst.Reference("res"), new ValueAst.Integer(1))),
                new ValueAst.String("b"), new ValueAst.Verb(CreateVerbCall("set", new ValueAst.Reference("res"), new ValueAst.Integer(2))),
                new ValueAst.String("c"), new ValueAst.Verb(CreateVerbCall("set", new ValueAst.Reference("res"), new ValueAst.Integer(3)))
            );

            var result = _context.ExecuteVerb(call);
            Assert.True(result.IsSuccess, string.Join(", ", result.DiagnosticsOrEmpty));

            if (result.ValueOrNothing is ZohVerb v) _context.ExecuteVerb(v.VerbValue.Call);

            Assert.Equal(2L, _context.Variables.Get("res").AsInt().Value);
        }

        [Fact]
        public void Switch_ExecutesDefault()
        {
            _context.Variables.Set("val", new ZohStr("z"));
            _context.Variables.Set("res", new ZohInt(0));

            // /switch val, "a", ..., default: $/set "res", 99
            var call = CreateVerbCall("switch",
                new ValueAst.Reference("val"),
                new ValueAst.String("a"), new ValueAst.Verb(CreateVerbCall("set", new ValueAst.Reference("res"), new ValueAst.Integer(1))),
                new ValueAst.Verb(CreateVerbCall("set", new ValueAst.Reference("res"), new ValueAst.Integer(99))) // Default
            );

            var result = _context.ExecuteVerb(call);
            Assert.True(result.IsSuccess, string.Join(", ", result.DiagnosticsOrEmpty));

            if (result.ValueOrNothing is ZohVerb v) _context.ExecuteVerb(v.VerbValue.Call);

            Assert.Equal(99L, _context.Variables.Get("res").AsInt().Value);
        }

        [Fact]
        public void Loop_Iterations()
        {
            _context.Variables.Set("count", new ZohInt(0));
            // /loop 5, /verb;;
            var call = CreateVerbCall("loop",
                new ValueAst.Integer(5),
                new ValueAst.Verb(CreateVerbCall("increase", new ValueAst.Reference("count")))
            );

            _context.ExecuteVerb(call);

            Assert.Equal(5L, _context.Variables.Get("count").AsInt().Value);
        }

        [Fact]
        public void Switch_ReturnsValue()
        {
            // /switch "a", "a", 100, "b", 200; -> should return 100

            var call = CreateVerbCall("switch",
                new ValueAst.String("a"),
                new ValueAst.String("a"),
                new ValueAst.Integer(100),
                new ValueAst.String("b"),
                new ValueAst.Integer(200)
            );

            var result = _context.ExecuteVerb(call);
            Assert.True(result.IsSuccess, string.Join(", ", result.DiagnosticsOrEmpty));
            Assert.Equal(100L, result.ValueOrNothing.AsInt().Value);
        }

        [Fact]
        public void Loop_BreakIf()
        {
            _context.Variables.Set("count", new ZohInt(0));
            // /loop [breakif: *stop] /sequence $/increase count, $/check_count

            _context.RegisterDriver("check_count", new CheckCountDriver());

            var loopCall = CreateVerbCall("loop",
                new ValueAst.Integer(10),
                new ValueAst.Verb(
                    CreateVerbCall("sequence",
                        new ValueAst.Verb(CreateVerbCall("increase", new ValueAst.Reference("count"))),
                        new ValueAst.Verb(CreateVerbCall("check_count"))
                    )
                )
            );

            loopCall = AddNamedParam(loopCall, "breakif", new ValueAst.Reference("stop"));

            _context.Variables.Set("stop", new ZohBool(false));

            _context.ExecuteVerb(loopCall);

            Assert.Equal(3L, _context.Variables.Get("count").AsInt().Value);
        }

        [Fact]
        public void While_Condition()
        {
            _context.Variables.Set("x", new ZohInt(0));
            // /while *cond, /sequence $/increase x, $/update_cond

            _context.RegisterDriver("update_cond", new UpdateCondDriver());

            var call = CreateVerbCall("while",
                new ValueAst.Reference("cond"),
                new ValueAst.Verb(
                     CreateVerbCall("sequence",
                        new ValueAst.Verb(CreateVerbCall("increase", new ValueAst.Reference("x"))),
                        new ValueAst.Verb(CreateVerbCall("update_cond"))
                    )
                )
            );

            _context.Variables.Set("cond", new ZohBool(true));

            _context.ExecuteVerb(call);

            Assert.Equal(3L, _context.Variables.Get("x").AsInt().Value);
        }

        [Fact]
        public void While_TypeCheck()
        {
            _context.Variables.Set("val", new ZohInt(5));
            // /while *val, is: "integer", /sequence $/increase val, $/check_type

            // We need a custom driver to change type of val to stop loop
            _context.RegisterDriver("change_type", new ChangeTypeDriver());

            var call = CreateVerbCall("while",
                new ValueAst.Reference("val"),
                new ValueAst.Verb(
                     CreateVerbCall("sequence",
                        new ValueAst.Verb(CreateVerbCall("increase", new ValueAst.String("val"))),
                        new ValueAst.Verb(CreateVerbCall("change_type"))
                    )
                )
            );
            call = AddNamedParam(call, "is", new ValueAst.String("integer"));

            _context.ExecuteVerb(call);

            // Loop should run until val is no longer integer.
            // Loop runs: 
            // 1. val=5 (int). Matches "integer". Body runs. Increase val->6. ChangeType runs (checks if 6>7 -> change to string).
            // 2. val=6. Matches. Body runs. Increase->7. ChangeType checks 7>7 (False).
            // Wait, I need logic.

            Assert.Equal("stopped", _context.Variables.Get("val").AsString().Value);
        }

        [Fact]
        public void Foreach_List()
        {
            _context.Variables.Set("sum", new ZohInt(0));
            var list = new ZohList(ImmutableArray.Create<ZohValue>(new ZohInt(1), new ZohInt(2), new ZohInt(3)));
            _context.Variables.Set("mylist", list);

            // /foreach mylist, "item", $/increase sum, item
            var call = CreateVerbCall("foreach",
                new ValueAst.Reference("mylist"),
                new ValueAst.String("item"),
                new ValueAst.Verb(
                    CreateVerbCall("increase", new ValueAst.Reference("sum"), new ValueAst.Reference("item"))
                )
            );

            _context.ExecuteVerb(call);

            Assert.Equal(6L, _context.Variables.Get("sum").AsInt().Value);
        }

        [Fact]
        public void Sequence_ExecutesInOrder()
        {
            _context.Variables.Set("x", new ZohInt(0));
            // /sequence $/set x 1, $/increase x
            var call = CreateVerbCall("sequence",
                new ValueAst.Verb(CreateVerbCall("set", new ValueAst.Reference("x"), new ValueAst.Integer(1))),
                new ValueAst.Verb(CreateVerbCall("increase", new ValueAst.Reference("x")))
            );

            _context.ExecuteVerb(call);

            Assert.Equal(2L, _context.Variables.Get("x").AsInt().Value);
        }

        // Helpers
        private VerbCallAst CreateVerbCall(string name, params ValueAst[] unnamedParams)
        {
            return new VerbCallAst(
                null,
                name,
                false,
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

        // Mock Drivers for side effects
        class CheckCountDriver : IVerbDriver
        {
            public string Namespace => "test";
            public string Name => "check_count";
            public DriverResult Execute(IExecutionContext context, VerbCallAst call)
            {
                var count = context.Variables.Get("count").AsInt().Value;
                if (count == 3) context.Variables.Set("stop", new ZohBool(true));
                return DriverResult.Complete.Ok();
            }
        }

        class UpdateCondDriver : IVerbDriver
        {
            public string Namespace => "test";
            public string Name => "update_cond";
            public DriverResult Execute(IExecutionContext context, VerbCallAst call)
            {
                var x = context.Variables.Get("x").AsInt().Value;
                if (x >= 3) context.Variables.Set("cond", new ZohBool(false));
                return DriverResult.Complete.Ok();
            }
        }
        class ChangeTypeDriver : IVerbDriver
        {
            public string Namespace => "test";
            public string Name => "change_type";
            public DriverResult Execute(IExecutionContext context, VerbCallAst call)
            {
                var val = context.Variables.Get("val");
                if (val is ZohInt i && i.Value > 7)
                {
                    context.Variables.Set("val", new ZohStr("stopped"));
                }
                return DriverResult.Complete.Ok();
            }
        }

        class IfOrderSubjectDriver : IVerbDriver
        {
            public string Namespace => "test";
            public string Name => "if_order_subject";
            public DriverResult Execute(IExecutionContext context, VerbCallAst call)
            {
                context.Variables.Set("order", new ZohInt(1));
                return DriverResult.Complete.Ok(new ZohBool(true));
            }
        }

        class IfOrderThenDriver : IVerbDriver
        {
            public string Namespace => "test";
            public string Name => "if_order_then";
            public DriverResult Execute(IExecutionContext context, VerbCallAst call)
            {
                var order = context.Variables.Get("order");
                if (order is not ZohInt i || i.Value != 1L)
                {
                    return DriverResult.Complete.Fatal(new Diagnostic(
                        DiagnosticSeverity.Fatal,
                        "test_failed",
                        "then branch ran before subject or order mismatch",
                        call.Start));
                }
                context.Variables.Set("x", new ZohInt(1));
                return DriverResult.Complete.Ok();
            }
        }

        class ReturnInt42Driver : IVerbDriver
        {
            public string Namespace => "test";
            public string Name => "return_int42";
            public DriverResult Execute(IExecutionContext context, VerbCallAst call)
                => DriverResult.Complete.Ok(new ZohInt(42));
        }
    }
}
