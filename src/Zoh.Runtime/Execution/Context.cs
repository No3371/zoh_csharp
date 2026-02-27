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
    public Func<IExecutionContext, VerbCallAst, VerbResult>? StatementExecutor { get; set; }
    public Func<string, CompiledStory?>? StoryLoader { get; set; }
    public Action<Context>? ContextScheduler { get; set; }

    public VerbResult ExecuteVerb(ValueAst verb, IExecutionContext context)
    {
        return VerbExecutor?.Invoke(verb, context) ?? VerbResult.Ok();
    }

    public void Run()
    {
        while (State == ContextState.Running)
        {
            if (CurrentStory == null || InstructionPointer >= CurrentStory.Statements.Length)
            {
                Terminate();
                break;
            }

            var stmt = CurrentStory.Statements[InstructionPointer];

            // Capture state before execution to detect jumps
            int entryIp = InstructionPointer;
            CompiledStory entryStory = CurrentStory;

            if (stmt is StatementAst.VerbCall callStmt)
            {
                var call = callStmt.Call;
                var result = StatementExecutor!(this, call);

                if (!result.IsSuccess)
                {
                    if (result.Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Fatal))
                    {
                        LastDiagnostics = result.Diagnostics;
                        SetState(ContextState.Terminated);
                        break;
                    }
                }
                LastResult = result.Value;
                LastDiagnostics = result.Diagnostics;

                if (result.Continuation != null)
                {
                    Block(result.Continuation);
                    break;
                }
            }
            else if (stmt is StatementAst.Label label)
            {
                var validation = ValidateContract(label.Name);
                if (!validation.IsSuccess)
                {
                    if (validation.Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Fatal))
                    {
                        LastDiagnostics = validation.Diagnostics;
                        SetState(ContextState.Terminated);
                        break;
                    }
                }
            }

            // If still running and didn't jump, advance IP
            if (State == ContextState.Running &&
                InstructionPointer == entryIp &&
                CurrentStory == entryStory)
            {
                InstructionPointer++;
            }
        }
    }

    public ChannelManager ChannelManager { get; }
    public SignalManager SignalManager { get; }

    public Context(VariableStore variables, IPersistentStorage storage, ChannelManager channels, SignalManager signalManager)
    {
        Variables = variables;
        Storage = storage;
        ChannelManager = channels;
        SignalManager = signalManager;
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

        SignalManager.UnsubscribeContext(this);

        State = ContextState.Terminated;
    }

    /// <summary>
    /// Applies a blocking continuation returned by a verb driver.
    /// Sets context state and handles all registrations (signal subscribe, etc.).
    /// This is the only place context state is set for blocking — drivers must
    /// return a VerbContinuation instead of calling SetState() directly.
    /// </summary>
    public void Block(VerbContinuation continuation)
    {
        switch (continuation)
        {
            case SleepContinuation s:
                WaitCondition = DateTimeOffset.UtcNow.AddMilliseconds(s.DurationMs);
                SetState(ContextState.Sleeping);
                break;

            case MessageContinuation m:
                SignalManager.Subscribe(m.MessageName, this);
                WaitCondition = m.MessageName;
                SetState(ContextState.WaitingMessage);
                break;

            case ContextContinuation c:
                WaitCondition = c.ChildContext;
                SetState(ContextState.WaitingContext);
                break;

            case HostContinuation h:
                WaitCondition = h.InteractionType;
                SetState(ContextState.WaitingHost);
                break;

            default:
                throw new InvalidOperationException($"Unhandled continuation type: {continuation.GetType().Name}");
        }
    }

    /// <summary>
    /// Resumes a context that was blocked by a host continuation (e.g., waiting for UI input).
    /// </summary>
    public void Resume(ZohValue? value = null)
    {
        if (State == ContextState.Terminated) return;

        LastResult = value ?? ZohNothing.Instance;
        WaitCondition = null;
        SetState(ContextState.Running);
    }

    private void ExecuteDefers(Stack<ValueAst> defers)
    {
        while (defers.Count > 0)
        {
            var verb = defers.Pop();
            VerbExecutor?.Invoke(verb, this);
        }
    }

    public void ExitStory()
    {
        ExecuteDefers(_storyDefers);
        Variables.ClearStory();
    }

    public IPersistentStorage Storage { get; }

    public int InstructionPointer { get; set; }
    public CompiledStory? CurrentStory { get; set; }
    public object? WaitCondition { get; set; }

    public Context Clone()
    {
        var newVars = Variables.Clone();
        var newContext = new Context(newVars, Storage, ChannelManager, SignalManager)
        {
            InstructionPointer = InstructionPointer,
            CurrentStory = CurrentStory,
            VerbExecutor = VerbExecutor,
            StatementExecutor = StatementExecutor,
            StoryLoader = StoryLoader,
            ContextScheduler = ContextScheduler,
            LastResult = LastResult // Should we copy last result? Probably harmless.
        };
        return newContext;
    }

    public VerbResult ValidateContract(string checkpointName)
    {
        if (CurrentStory != null &&
            CurrentStory.Contracts.TryGetValue(checkpointName, out var paramsList))
        {
            foreach (var param in paramsList)
            {
                var val = Variables.Get(param.Name);
                if (val is ZohNothing)
                {
                    return VerbResult.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "checkpoint_violation", $"Contract violation: Variable '{param.Name}' is Nothing at checkpoint '@{checkpointName}'.", param.Position));
                }

                if (param.Type != null)
                {
                    if (!CheckType(val, param.Type))
                    {
                        return VerbResult.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "checkpoint_violation", $"Contract violation: Variable '{param.Name}' is not of type '{param.Type}' (got '{val.GetType().Name}') at checkpoint '@{checkpointName}'.", param.Position));
                    }
                }
            }
        }
        return VerbResult.Ok();
    }

    private bool CheckType(ZohValue val, string type)
    {
        return (type.ToLowerInvariant()) switch
        {
            "string" => val is ZohStr,
            "integer" => val is ZohInt,
            "boolean" => val is ZohBool,
            "double" => val is ZohFloat,
            "list" => val is ZohList,
            "map" => val is ZohMap,
            "channel" => val is ZohChannel,
            "verb" => val is ZohVerb,
            "expression" => val is ZohExpr,
            _ => false
        };
    }
}
