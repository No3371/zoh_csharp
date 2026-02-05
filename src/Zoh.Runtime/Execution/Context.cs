using Zoh.Runtime.Expressions;
using Zoh.Runtime.Variables;
using Zoh.Runtime.Types;
using Zoh.Runtime.Parsing.Ast;
using Zoh.Runtime.Diagnostics;
using System.Collections.Immutable;

using Zoh.Runtime.Verbs;
using Zoh.Runtime.Storage;

namespace Zoh.Runtime.Execution;

public class Context : IExecutionContext
{
    public VariableStore Variables { get; }
    public ExpressionEvaluator Evaluator { get; }

    public ZohValue LastResult { get; set; } = ZohValue.Nothing;
    public ContextState State { get; private set; } = ContextState.Running;

    private readonly Stack<ValueAst> _storyDefers = new();
    private readonly Stack<ValueAst> _contextDefers = new();

    public IList<Diagnostic> LastDiagnostics { get; set; } = ImmutableList<Diagnostic>.Empty;

    // To be injected or set by Runtime
    public Func<ValueAst, IExecutionContext, VerbResult>? VerbExecutor { get; set; }

    public VerbResult ExecuteVerb(ValueAst verb, IExecutionContext context)
    {
        return VerbExecutor?.Invoke(verb, context) ?? VerbResult.Ok();
    }

    public Context(VariableStore variables, IPersistentStorage storage)
    {
        Variables = variables;
        Storage = storage;
        // Break circular dependency by injecting interpolator factory
        Evaluator = new ExpressionEvaluator(Variables);
    }

    public void SetState(ContextState state)
    {
        State = state;
    }

    public void AddStoryDefer(ValueAst verb) => _storyDefers.Push(verb);
    public void AddContextDefer(ValueAst verb) => _contextDefers.Push(verb);

    public void Terminate()
    {
        if (State == ContextState.Terminated) return;

        // Execute Defers (LIFO)
        ExecuteDefers(_storyDefers);
        ExecuteDefers(_contextDefers);

        State = ContextState.Terminated;
    }

    private void ExecuteDefers(Stack<ValueAst> defers)
    {
        while (defers.Count > 0)
        {
            var verb = defers.Pop();
            VerbExecutor?.Invoke(verb, this);
        }
    }

    public Func<string, int>? ChannelCountResolver { get; set; }

    public IPersistentStorage Storage { get; }

    public int GetChannelSize(string name)
    {
        return ChannelCountResolver?.Invoke(name) ?? 0;
    }
}
