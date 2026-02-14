using Zoh.Runtime.Diagnostics;

namespace Zoh.Runtime.Execution;

/// <summary>
/// Thrown when the compilation pipeline fails.
/// </summary>
public class CompilationException : Exception
{
    public DiagnosticBag Diagnostics { get; }

    public CompilationException(string message, DiagnosticBag diagnostics)
        : base(message)
    {
        Diagnostics = diagnostics;
    }
}
