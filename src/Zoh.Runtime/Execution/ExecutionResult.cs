namespace Zoh.Runtime.Execution;

using Zoh.Runtime.Types;
using Zoh.Runtime.Diagnostics;

/// <summary>
/// Result of a terminated context. Provides the final return value,
/// collected diagnostics, and lazy variable access.
/// </summary>
public class ExecutionResult
{
    private readonly Context _context;

    internal ExecutionResult(Context context)
    {
        if (context.State != ContextState.Terminated)
            throw new InvalidOperationException(
                "ExecutionResult is only valid for terminated contexts.");
        _context = context;
    }

    public ZohValue Value => _context.LastResult;
    public IReadOnlyList<Diagnostic> Diagnostics => (IReadOnlyList<Diagnostic>)_context.LastDiagnostics;
    public VariableAccessor Variables => new(_context.Variables);
}
