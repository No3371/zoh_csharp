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

    public Context CreateContext(CompiledStory story)
    {
        var store = new VariableStore(new Dictionary<string, Variable>());
        var ctx = new Context(store, Storage, Channels, SignalManager);

        ctx.VerbExecutor = ExecuteVerb;
        ctx.StatementExecutor = ExecuteStatement;
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

    private VerbResult ExecuteStatement(IExecutionContext ctx, VerbCallAst call)
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
                return VerbResult.Fatal(new Diagnostic(ex.Severity, ex.DiagnosticCode, ex.Message, call.Start));
            }
            catch (Exception ex)
            {
                return VerbResult.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "runtime_error", $"Unhandled exception: {ex.Message}", call.Start));
            }
        }
        return VerbResult.Ok();
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

    public void Run(Context ctx)
    {
        ctx.Run();
    }

    public ZohValue RunToCompletion(Context ctx)
    {
        Run(ctx);
        return ctx.LastResult ?? ZohNothing.Instance;
    }
}
