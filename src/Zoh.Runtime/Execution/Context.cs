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
    public string Id { get; } = Guid.NewGuid().ToString();

    public VariableStore Variables { get; }
    public ExpressionEvaluator Evaluator { get; }

    public ZohValue LastResult { get; set; } = ZohValue.Nothing;
    public ContextState State { get; private set; } = ContextState.Running;

    private readonly Stack<ValueAst> _storyDefers = new();
    private readonly Stack<ValueAst> _contextDefers = new();

    public IList<Diagnostic> LastDiagnostics { get; set; } = ImmutableList<Diagnostic>.Empty;

    // To be injected or set by Runtime
    public Func<ValueAst, IExecutionContext, DriverResult>? VerbExecutor { get; set; }
    public Func<IExecutionContext, VerbCallAst, DriverResult>? StatementExecutor { get; set; }
    public Func<string, CompiledStory?>? StoryLoader { get; set; }
    public Action<Context>? ContextScheduler { get; set; }

    public Continuation? PendingContinuation { get; private set; }
    public int ResumeToken { get; private set; }

    public DriverResult ExecuteVerb(ValueAst verb, IExecutionContext context)
    {
        return VerbExecutor?.Invoke(verb, context) ?? DriverResult.Complete.Ok();
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
                var result = StatementExecutor!(this, callStmt.Call);
                ApplyResult(result, entryIp, entryStory);
            }
            else if (stmt is StatementAst.Label label)
            {
                var validation = ValidateContract(label.Name);
                if (validation is DriverResult.Complete c &&
                    c.Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Fatal))
                {
                    LastDiagnostics = c.Diagnostics;
                    SetState(ContextState.Terminated);
                    break;
                }
                // Labels: advance IP if no jump
                if (State == ContextState.Running &&
                    InstructionPointer == entryIp &&
                    CurrentStory == entryStory)
                {
                    InstructionPointer++;
                }
            }
        }
    }

    private void ApplyResult(DriverResult result, int entryIp, CompiledStory entryStory)
    {
        switch (result)
        {
            case DriverResult.Complete c:
                LastResult = c.Value;
                LastDiagnostics = c.Diagnostics;
                if (c.Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Fatal))
                {
                    SetState(ContextState.Terminated);
                }
                else if (State == ContextState.Running &&
                         InstructionPointer == entryIp &&
                         CurrentStory == entryStory)
                {
                    InstructionPointer++;
                }
                break;

            case DriverResult.Suspend s:
                LastDiagnostics = s.Diagnostics;
                ResumeToken++;
                PendingContinuation = s.Continuation;
                BlockOnRequest(s.Continuation.Request);
                break;
        }
    }

    private void BlockOnRequest(WaitRequest request)
    {
        switch (request)
        {
            case SleepRequest s:
                WaitCondition = DateTimeOffset.UtcNow.AddMilliseconds(s.DurationMs);
                SetState(ContextState.Sleeping);
                break;

            case SignalRequest m:
                SignalManager.Subscribe(m.MessageName, this);
                WaitCondition = m.MessageName;
                SetState(ContextState.WaitingMessage);
                break;

            case JoinContextRequest c:
                WaitCondition = c.ContextId;
                SetState(ContextState.WaitingContext);
                break;

            case HostRequest h:
                WaitCondition = h.TimeoutMs;
                SetState(ContextState.WaitingHost);
                break;

            default:
                throw new InvalidOperationException($"Unhandled request type: {request.GetType().Name}");
        }
    }

    /// <summary>
    /// Resumes the context with the given outcome. Token must match current ResumeToken.
    /// Invokes the pending continuation's OnFulfilled callback if present.
    /// Falls back to direct state update if no continuation was registered (backward compat).
    /// </summary>
    public void Resume(WaitOutcome outcome, int token)
    {
        if (token != ResumeToken) return; // Stale or double-resume guard
        ResumeToken++; // Invalidate current token to prevent double-resume

        WaitCondition = null;

        if (PendingContinuation != null)
        {
            var handler = PendingContinuation.OnFulfilled;
            PendingContinuation = null;

            var result = handler(outcome);
            SetState(ContextState.Running);

            int ip = InstructionPointer;
            var story = CurrentStory!;
            ApplyResult(result, ip, story);
        }
        else
        {
            // Fallback: direct state update when no continuation registered (backward compat / tests)
            LastResult = outcome is WaitCompleted c ? c.Value : ZohNothing.Instance;
            SetState(ContextState.Running);
        }
    }

    /// <summary>
    /// Backward-compatible overload for existing host code and tests.
    /// </summary>
    public void Resume(ZohValue? value = null)
    {
        Resume(new WaitCompleted(value ?? ZohNothing.Instance), ResumeToken);
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
            LastResult = LastResult
            // ResumeToken and PendingContinuation start fresh (defaults: 0, null)
        };
        return newContext;
    }

    public DriverResult ValidateContract(string checkpointName)
    {
        if (CurrentStory != null &&
            CurrentStory.Contracts.TryGetValue(checkpointName, out var paramsList))
        {
            foreach (var param in paramsList)
            {
                var val = Variables.Get(param.Name);
                if (val is ZohNothing)
                {
                    return DriverResult.Complete.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "checkpoint_violation", $"Contract violation: Variable '{param.Name}' is Nothing at checkpoint '@{checkpointName}'.", param.Position));
                }

                if (param.Type != null)
                {
                    if (!CheckType(val, param.Type))
                    {
                        return DriverResult.Complete.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "checkpoint_violation", $"Contract violation: Variable '{param.Name}' is not of type '{param.Type}' (got '{val.GetType().Name}') at checkpoint '@{checkpointName}'.", param.Position));
                    }
                }
            }
        }
        return DriverResult.Complete.Ok();
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
