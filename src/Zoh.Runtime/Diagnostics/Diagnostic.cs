using Zoh.Runtime.Lexing;

namespace Zoh.Runtime.Diagnostics;

public sealed record Diagnostic(DiagnosticSeverity Severity, string Code, string Message, TextPosition Position, string? FilePath = null)
{
    public override string ToString() => $"[{Severity}] {Code}: {Message} at {Position} in {FilePath ?? "unknown"}";
}
