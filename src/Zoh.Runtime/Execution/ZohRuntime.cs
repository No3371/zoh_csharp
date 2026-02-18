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
        var compiled = CompiledStory.FromAst(parseResult.Story!);

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

    public Context CreateContext(CompiledStory story)
    {
        var store = new VariableStore(new Dictionary<string, Variable>());
        var ctx = new Context(store, Storage, Channels, SignalManager);

        ctx.VerbExecutor = ExecuteVerb;
        ctx.StoryLoader = GetCompiledStory;
        ctx.ContextScheduler = AddContext;
        ctx.CurrentStory = story;

        _contexts.Add(ctx);
        return ctx;
    }

    public void AddContext(Context ctx)
    {
        _contexts.Add(ctx);
    }

    public VerbResult ExecuteVerb(ValueAst verb, IExecutionContext ctx)
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

        if (call == null) return VerbResult.Ok(); // Or Error? "Nothing executed"

        var driver = VerbRegistry.GetDriver(call.Namespace, call.Name);
        if (driver != null)
        {
            try
            {
                return driver.Execute(ctx, call);
            }
            catch (ZohDiagnosticException ex)
            {
                return VerbResult.Fatal(new Diagnostic(ex.Severity, ex.DiagnosticCode, ex.Message, call.Start));
            }
            catch (Exception ex)
            {
                return VerbResult.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "runtime_error", $"Unhandled exception: {ex.Message}", call.Start));
            }
        }
        else
        {
            // Log warning
            // Return Failure? Or just Log?
            return VerbResult.Ok();
        }
    }

    public void Run(Context ctx, CompiledStory story)
    {
        // Initialize context story if needed
        if (ctx.CurrentStory == null)
        {
            ctx.CurrentStory = story;
        }

        while (ctx.State == ContextState.Running)
        {
            if (ctx.CurrentStory == null || ctx.InstructionPointer >= ctx.CurrentStory.Statements.Length)
            {
                ctx.Terminate();
                break;
            }

            var stmt = ctx.CurrentStory.Statements[ctx.InstructionPointer];

            // Capture state before execution to detect jumps
            int entryIp = ctx.InstructionPointer;
            CompiledStory entryStory = ctx.CurrentStory;

            if (stmt is StatementAst.VerbCall callStmt)
            {
                var call = callStmt.Call;
                var driver = VerbRegistry.GetDriver(call.Namespace, call.Name);
                if (driver != null)
                {
                    VerbResult result;
                    try
                    {
                        result = driver.Execute(ctx, call);
                    }
                    catch (Exception ex)
                    {
                        result = VerbResult.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "runtime_error", $"Unhandled exception: {ex.Message}", call.Start));
                    }

                    if (!result.IsSuccess)
                    {
                        // Check for Fatal diagnostics
                        if (result.Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Fatal))
                        {
                            ctx.LastDiagnostics = result.Diagnostics;
                            ctx.SetState(ContextState.Terminated); // Stop execution
                            break;
                        }
                    }
                    ctx.LastResult = result.Value;
                    ctx.LastDiagnostics = result.Diagnostics;
                }
            }
            else if (stmt is StatementAst.Label label)
            {
                // Validate contract on fallthrough or initial start
                var validation = ctx.ValidateContract(label.Name);
                if (!validation.IsSuccess)
                {
                    if (validation.Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Fatal))
                    {
                        ctx.LastDiagnostics = validation.Diagnostics;
                        ctx.SetState(ContextState.Terminated);
                        break;
                    }
                }
            }

            // If we are still running and didn't jump (IP and Story unchanged), advance IP
            if (ctx.State == ContextState.Running &&
                ctx.InstructionPointer == entryIp &&
                ctx.CurrentStory == entryStory)
            {
                ctx.InstructionPointer++;
            }
        }

        // Run returns when context is no longer Running (Terminated, Waiting*, Sleeping)
        // If Terminated, we ensure cleanup
        if (ctx.State == ContextState.Terminated)
        {
            // Already handled by Context.Terminate inside SetState or explicit call? 
            // Context.Terminate() sets state to Terminated and runs defers.
            // If we just break loop, we might need to ensure Terminate is called if we ran off end.
            // (Handled by the bounds check at top)
        }
    }
}
