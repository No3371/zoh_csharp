using Zoh.Runtime.Variables;
using Zoh.Runtime.Expressions;
using Zoh.Runtime.Types;
using Zoh.Runtime.Parsing.Ast;
using Zoh.Runtime.Diagnostics;
using Zoh.Runtime.Verbs;
using Zoh.Runtime.Storage;

namespace Zoh.Runtime.Execution;

/// <summary>
/// Minimal execution context required for verb drivers.
/// decouples drivers from the full Runtime/Context implementation.
/// </summary>
public interface IExecutionContext
{
    VariableStore Variables { get; }
    ExpressionEvaluator Evaluator { get; }

    ZohValue LastResult { get; set; }
    ContextState State { get; }

    // Defers
    void AddStoryDefer(ValueAst verb);
    void AddContextDefer(ValueAst verb); // Or CompiledVerbCall if we had compilation, using ValueAst for now as VerbValue/Reference

    // Diagnostics
    IList<Diagnostic> LastDiagnostics { get; set; }

    /// <summary>
    /// Execute a verb dynamically (e.g. for /do, /eval calls that return verbs, defers)
    /// </summary>
    DriverResult ExecuteVerb(ValueAst verb, IExecutionContext context);

    ChannelManager ChannelManager { get; }

    IPersistentStorage Storage { get; }
}
