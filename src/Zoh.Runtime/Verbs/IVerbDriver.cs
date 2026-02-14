using Zoh.Runtime.Execution;
using Zoh.Runtime.Parsing.Ast;

namespace Zoh.Runtime.Verbs;

public interface IVerbDriver
{
    string Namespace { get; }
    string Name { get; }

    /// <summary>
    /// Priority for driver selection. Lower values = higher priority.
    /// Core built-in handlers: int.MinValue to 0.
    /// High-priority extensions: 1–1000.
    /// Standard extensions: 1001–10000.
    /// Low-priority extensions: 10001+.
    /// </summary>
    int Priority => 0; // Default implementation for backward compat

    VerbResult Execute(IExecutionContext context, VerbCallAst verbCall);
}
