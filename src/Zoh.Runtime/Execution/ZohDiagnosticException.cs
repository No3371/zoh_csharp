using Zoh.Runtime.Diagnostics;
using Zoh.Runtime.Lexing; // For TextPosition if needed, though mostly 0,0,0

namespace Zoh.Runtime.Execution;

public class ZohDiagnosticException : Exception
{
    public string DiagnosticCode { get; }
    public DiagnosticSeverity Severity { get; }

    public ZohDiagnosticException(string code, string message, DiagnosticSeverity severity = DiagnosticSeverity.Fatal)
        : base(message)
    {
        DiagnosticCode = code;
        Severity = severity;
    }
}
