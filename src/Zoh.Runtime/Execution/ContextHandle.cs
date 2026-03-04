namespace Zoh.Runtime.Execution;

/// <summary>
/// Opaque handle to a context. The only representation visible to callers.
/// Exposes read-only state for host code to identify and track contexts.
/// Internal fields (IP, continuations, defers) are not accessible.
/// </summary>
public class ContextHandle
{
    private readonly Context _context;

    internal ContextHandle(Context context) => _context = context;

    public string Id => _context.Id;
    public ContextState State => _context.State;

    internal Context InternalContext => _context;
}
