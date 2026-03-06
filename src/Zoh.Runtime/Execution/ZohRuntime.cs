using System.Collections.Concurrent;
using Zoh.Runtime.Parsing;
using Zoh.Runtime.Parsing.Ast;
using Zoh.Runtime.Verbs;
using Zoh.Runtime.Lexing;
using Zoh.Runtime.Diagnostics;
using Zoh.Runtime.Types;
using Zoh.Runtime.Variables;
using System.Linq;
using Zoh.Runtime.Storage;
using Zoh.Runtime.Validation;
using Zoh.Runtime.Preprocessing;

namespace Zoh.Runtime.Execution;

public class ZohRuntime
{
    public RuntimeConfig Config { get; }
    public HandlerRegistry Handlers { get; }
    public ChannelManager Channels { get; } = new();
    public SignalManager SignalManager { get; } = new();
    public Storage.IPersistentStorage Storage { get; set; }

    // Backward compat: keep VerbRegistry accessor
    public VerbRegistry VerbRegistry => Handlers.VerbDrivers;

    public IReadOnlyList<Context> Contexts => _contexts;
    private readonly List<Context> _contexts = new();
    private readonly Dictionary<string, CompiledStory> _storyCache = new();
    private double _elapsedMs;
    private readonly Dictionary<string, ContextHandle> _handles = new();

    internal double ElapsedMs => _elapsedMs;

    public IReadOnlyCollection<ContextHandle> Handles => _handles.Values;

    public ZohRuntime() : this(RuntimeConfig.Default) { }

    public ZohRuntime(RuntimeConfig config)
    {
        Config = config;
        Storage = new Storage.InMemoryStorage();
        Handlers = new HandlerRegistry();
        Handlers.RegisterCoreHandlers();
    }

    /// <summary>
    /// Loads a story through the compilation pipeline:
    /// preprocess → lex → parse → compile → validate.
    /// </summary>
    public CompiledStory LoadStory(string source, string sourcePath = "")
    {
        var diagnostics = new DiagnosticBag();

        // 1. Preprocess
        string processed = source;
        foreach (var pp in Handlers.Preprocessors)
        {
            var ctx = new PreprocessorContext(processed, sourcePath);
            var result = pp.Process(ctx);
            diagnostics.AddRange(result.Diagnostics);
            if (diagnostics.HasFatalErrors)
                throw new CompilationException("Preprocessing failed", diagnostics);
            processed = result.ProcessedText;
        }

        // 2. Lex
        var lexer = new Lexer(processed, true);
        var tokens = lexer.Tokenize();
        if (tokens.HasErrors)
            throw new CompilationException("Lexing failed: " + string.Join(", ", tokens.Errors), diagnostics);

        // 3. Parse
        var parser = new Parser(tokens.Tokens);
        var parseResult = parser.Parse();
        if (!parseResult.Success)
            throw new CompilationException("Parsing failed: " + string.Join(", ", parseResult.Errors), diagnostics);

        // 4. Compile (currently: wrap AST)
        var compiled = CompiledStory.FromAst(parseResult.Story!, diagnostics);
        if (diagnostics.HasErrors)
            throw new CompilationException("Compilation failed", diagnostics);

        // 5. Validate
        // 5a. Story validators
        foreach (var validator in Handlers.StoryValidators)
        {
            var valDiags = validator.Validate(compiled);
            diagnostics.AddRange(valDiags);
        }

        // 5b. Check for fatal diagnostics from story validators
        if (diagnostics.HasFatalErrors)
            throw new CompilationException("Validation failed", diagnostics);

        _storyCache[compiled.Name] = compiled;
        return compiled;
    }

    public CompiledStory? GetCompiledStory(string name)
    {
        if (_storyCache.TryGetValue(name, out var story)) return story;
        return null;
    }

    private Context CreateContextInternal(CompiledStory story)
    {
        var store = new VariableStore(new Dictionary<string, Variable>());
        var ctx = new Context(store, Storage, Channels, SignalManager);

        ctx.VerbExecutor = ExecuteVerb;
        ctx.StatementExecutor = ExecuteStatement;
        ctx.StoryLoader = GetCompiledStory;
        ctx.ContextScheduler = AddContext;
        ctx.ElapsedMsProvider = () => _elapsedMs;
        ctx.CurrentStory = story;

        var handle = new ContextHandle(ctx);
        ctx.Handle = handle;
        _handles[ctx.Id] = handle;
        _contexts.Add(ctx);
        return ctx;
    }

    [Obsolete("Use StartContext() which returns a ContextHandle.")]
    public Context CreateContext(CompiledStory story)
    {
        return CreateContextInternal(story);
    }

    public void AddContext(Context ctx)
    {
        ctx.Handle ??= new ContextHandle(ctx);
        _handles[ctx.Id] = ctx.Handle;
        _contexts.Add(ctx);
    }

    /// <summary>
    /// Creates a new context for the given story and returns an opaque handle.
    /// The context begins in Running state and will execute on the next Tick().
    /// </summary>
    public ContextHandle StartContext(CompiledStory story)
    {
        var ctx = CreateContextInternal(story);
        return ctx.Handle!;
    }

    /// <summary>
    /// Advances the runtime by deltaTimeMs. Accumulates elapsed time, resolves
    /// blocked contexts whose wait conditions are met, then runs all RUNNING contexts.
    /// </summary>
    public void Tick(double deltaTimeMs)
    {
        _elapsedMs += deltaTimeMs;

        for (int i = 0; i < _contexts.Count; i++)
        {
            var ctx = _contexts[i];

            if (ctx.State != ContextState.Running && ctx.State != ContextState.Terminated)
            {
                var token = ctx.ResumeToken;
                var outcome = ResolveWait(ctx);
                if (outcome != null)
                {
                    ctx.Resume(outcome, token);
                }
            }

            if (ctx.State == ContextState.Running)
            {
                ctx.Run();
            }
        }
    }

    /// <summary>
    /// Checks if a blocked context's wait condition is met.
    /// Returns WaitOutcome if ready to resume, null if still waiting.
    /// </summary>
    private WaitOutcome? ResolveWait(Context ctx)
    {
        switch (ctx.State)
        {
            case ContextState.Sleeping:
                if (ctx.WaitCondition is SleepCondition sleep && _elapsedMs >= sleep.WakeTimeMs)
                    return new WaitCompleted(ZohValue.Nothing);
                return null;

            case ContextState.WaitingHost:
                // Host-driven: scheduler only handles timeout.
                // Host calls runtime.Resume(handle, value) to fulfill.
                if (ctx.WaitCondition is HostWaitCondition host && host.IsTimedOut(_elapsedMs))
                    return new WaitTimedOut();
                return null;

            case ContextState.WaitingMessage:
                // Fulfillment handled by SignalManager.Broadcast (fast path).
                // Scheduler only handles timeout.
                if (ctx.WaitCondition is SignalWaitCondition sig && sig.IsTimedOut(_elapsedMs))
                {
                    SignalManager.Unsubscribe(sig.MessageName, ctx);
                    return new WaitTimedOut();
                }
                return null;

            case ContextState.WaitingContext:
                if (ctx.WaitCondition is ContextJoinCondition join)
                {
                    var target = join.TargetHandle;
                    if (target.State == ContextState.Terminated)
                        return new WaitCompleted(target.InternalContext.LastResult);
                }
                return null;

            case ContextState.WaitingChannel:
                // Value delivery handled by PushDriver fast path.
                // Scheduler only handles timeout.
                if (ctx.WaitCondition is ChannelWaitCondition chan && chan.IsTimedOut(_elapsedMs))
                    return new WaitTimedOut();
                return null;

            default:
                return null;
        }
    }

    /// <summary>
    /// Resumes a WAITING_HOST context with the given value.
    /// The only public path for host code to unblock a suspended context.
    /// </summary>
    public void Resume(ContextHandle handle, ZohValue value)
    {
        var ctx = handle.InternalContext;
        var token = ctx.ResumeToken;
        ctx.Resume(new WaitCompleted(value), token);
    }

    /// <summary>
    /// Returns the execution result for a terminated context.
    /// Throws if the context has not terminated.
    /// </summary>
    public ExecutionResult GetResult(ContextHandle handle)
    {
        return new ExecutionResult(handle.InternalContext);
    }

    private DriverResult ExecuteStatement(IExecutionContext ctx, VerbCallAst call)
    {
        var driver = VerbRegistry.GetDriver(call.Namespace, call.Name);
        if (driver != null)
        {
            try
            {
                return driver.Execute(ctx, call);
            }
            catch (ZohDiagnosticException ex)
            {
                return DriverResult.Complete.Fatal(new Diagnostic(ex.Severity, ex.DiagnosticCode, ex.Message, call.Start));
            }
            catch (Exception ex)
            {
                return DriverResult.Complete.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "runtime_error", $"Unhandled exception: {ex.Message}", call.Start));
            }
        }
        return DriverResult.Complete.Ok();
    }

    public DriverResult ExecuteVerb(ValueAst verb, IExecutionContext ctx)
    {
        VerbCallAst? call = null;

        if (verb is ValueAst.Verb vv)
        {
            call = vv.Call;
        }
        else if (verb is ValueAst.Reference r)
        {
            var val = ValueResolver.Resolve(r, ctx);
            if (val is ZohVerb zv)
            {
                call = zv.VerbValue.Call;
            }
        }

        if (call == null) return DriverResult.Complete.Ok(); // Or Error? "Nothing executed"

        var driver = VerbRegistry.GetDriver(call.Namespace, call.Name);
        if (driver != null)
        {
            try
            {
                return driver.Execute(ctx, call);
            }
            catch (ZohDiagnosticException ex)
            {
                return DriverResult.Complete.Fatal(new Diagnostic(ex.Severity, ex.DiagnosticCode, ex.Message, call.Start));
            }
            catch (Exception ex)
            {
                return DriverResult.Complete.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "runtime_error", $"Unhandled exception: {ex.Message}", call.Start));
            }
        }
        else
        {
            // Log warning
            // Return Failure? Or just Log?
            return DriverResult.Complete.Ok();
        }
    }

    [Obsolete("Use Tick() to drive execution.")]
    public void Run(Context ctx)
    {
        ctx.Run();
    }

    [Obsolete("Use StartContext() + Tick().")]
    public ZohValue RunToCompletion(Context ctx)
    {
        Run(ctx);
        return ctx.LastResult ?? ZohNothing.Instance;
    }

    [Obsolete("Use StartContext() + Tick().")]
    public Context RunToCompletion(string source)
    {
        var story = LoadStory(source);
        var ctx = CreateContext(story);
        Run(ctx);
        return ctx;
    }
}
