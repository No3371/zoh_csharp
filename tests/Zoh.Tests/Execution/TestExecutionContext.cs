using System;
using Zoh.Runtime.Storage;
using System.Collections.Generic;
using Zoh.Runtime.Execution;
using Zoh.Runtime.Expressions;
using Zoh.Runtime.Types;
using Zoh.Runtime.Variables;
using Zoh.Runtime.Parsing.Ast;
using Zoh.Runtime.Diagnostics;
using Zoh.Runtime.Verbs;
using Zoh.Runtime.Lexing;


namespace Zoh.Tests.Execution;

public class TestExecutionContext : IExecutionContext
{
    public VariableStore Variables { get; }
    public IPersistentStorage Storage { get; } = new InMemoryStorage();
    public ExpressionEvaluator Evaluator { get; }
    public ZohValue LastResult { get; set; } = ZohValue.Nothing;
    public IList<Diagnostic> LastDiagnostics { get; set; } = new List<Diagnostic>();
    public Func<ValueAst, IExecutionContext, DriverResult>? VerbExecutor { get; set; }

    // Driver Registry
    private readonly Dictionary<string, IVerbDriver> _drivers = new(StringComparer.OrdinalIgnoreCase);

    public void RegisterDriver(string name, IVerbDriver driver)
    {
        _drivers[name] = driver;
    }

    public ChannelManager ChannelManager { get; } = new();

    public DriverResult ExecuteVerb(ValueAst verb, IExecutionContext context)
    {
        // Resolve ValueAst to ZohVerb
        // If it's a Verb ValueAst, we can execute it.
        // If it's a reference, resolve it.

        // Simulating ValueResolver resolution slightly or assuming passed ValueAst IS the verb wrapper
        // The drivers pass `thenVerb.VerbValue` which is `ValueAst.Verb`.

        // Check Mock Executor first
        if (VerbExecutor != null)
        {
            return VerbExecutor(verb, context);
        }

        if (verb is ValueAst.Verb v)
        {
            return ExecuteVerb(v.Call);
        }

        // If it's a reference, we might need to resolve it against variables?
        // For tests, usually we explicitly construct VerbValue.

        return DriverResult.Complete.Fatal(new Diagnostic(DiagnosticSeverity.Error, "InvalidVerb", $"Cannot execute {verb}", new TextPosition(0, 0, 0)));
    }

    public DriverResult ExecuteVerb(VerbCallAst call)
    {
        if (_drivers.TryGetValue(call.Name, out var driver))
        {
            return driver.Execute(this, call);
        }
        return DriverResult.Complete.Fatal(new Diagnostic(DiagnosticSeverity.Error, "VerbNotFound", $"Verb {call.Name} not found", call.Start));
    }

    public TestExecutionContext()
    {
        Variables = new VariableStore(new Dictionary<string, Variable>());
        Evaluator = new ExpressionEvaluator(Variables);
    }

    public TestExecutionContext(VariableStore variables)
    {
        Variables = variables;
        Evaluator = new ExpressionEvaluator(Variables);
    }

    public ContextState State { get; private set; } = ContextState.Running;
    public void SetState(ContextState state) => State = state;

    public void AddStoryDefer(ValueAst verb) { }
    public void AddContextDefer(ValueAst verb) { }
}
