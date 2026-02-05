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

namespace Zoh.Runtime.Execution;

public class ZohRuntime
{
    public VerbRegistry VerbRegistry { get; } = new();
    public ChannelManager Channels { get; } = new();
    public Storage.IPersistentStorage Storage { get; } = new Storage.InMemoryStorage();

    private readonly List<Context> _contexts = new();
    private readonly Dictionary<string, CompiledStory> _storyCache = new();

    public ZohRuntime()
    {
        VerbRegistry.RegisterCoreVerbs();
    }

    public CompiledStory LoadStory(string source, string storyName)
    {
        // 1. Lex
        var lexer = new Lexer(source);
        var tokens = lexer.Tokenize();
        if (tokens.HasErrors)
        {
            throw new Exception("Lexing failed: " + string.Join(", ", tokens.Errors));
        }

        // 2. Parse
        var parser = new Parser(tokens.Tokens);
        var parseResult = parser.Parse();
        if (!parseResult.Success)
        {
            throw new Exception("Parsing failed: " + string.Join(", ", parseResult.Errors));
        }
        var ast = parseResult.Story!;



        // 3. Validate
        var validator = new NamespaceValidator(VerbRegistry);
        var valResult = validator.Validate(ast);
        if (!valResult.IsSuccess)
        {
            throw new Exception("Validation failed: " + string.Join(", ", valResult.Errors.Select(e => e.Message)));
        }

        // 4. Compile (Wrap)
        var compiled = CompiledStory.FromAst(ast);
        _storyCache[storyName] = compiled;
        return compiled;
    }

    public Context CreateContext(CompiledStory story)
    {
        var store = new VariableStore(new Dictionary<string, Variable>());
        var ctx = new Context(store, Storage);

        ctx.VerbExecutor = ExecuteVerb;
        _contexts.Add(ctx);
        return ctx;
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
        int ip = 0;
        int len = story.Statements.Length;

        while (ctx.State == ContextState.Running && ip < len)
        {
            var stmt = story.Statements[ip];
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
            else if (stmt is StatementAst.Label)
            {
                // no-op
            }

            if (ctx.State != ContextState.Running) break;
            ip++;
        }

        if (ctx.State == ContextState.Running)
        {
            ctx.Terminate();
        }
    }
}
