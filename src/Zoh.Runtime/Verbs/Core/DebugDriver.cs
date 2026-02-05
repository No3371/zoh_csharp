using Zoh.Runtime.Execution;
using Zoh.Runtime.Parsing.Ast;
using Zoh.Runtime.Types;
using Zoh.Runtime.Diagnostics;
using System.Text;

namespace Zoh.Runtime.Verbs.Core;

public class DebugDriver : IVerbDriver
{
    public string Namespace => "core";
    public string Name => "log"; // Primary name. Aliases handled by Registry or manual registration.

    public VerbResult Execute(IExecutionContext context, VerbCallAst verb)
    {
        // Aliases: /log, /info, /warn, /error, /debug
        // Determine severity from verb name (case insensitive)
        var verbName = verb.Name.ToLowerInvariant();
        DiagnosticSeverity severity = DiagnosticSeverity.Info;

        switch (verbName)
        {
            case "warn":
            case "warning":
                severity = DiagnosticSeverity.Warning;
                break;
            case "error":
            case "fatal":
                severity = DiagnosticSeverity.Error;
                break;
            case "debug":
            case "info":
            case "log":
            default:
                severity = DiagnosticSeverity.Info;
                break;
        }

        var sb = new StringBuilder();
        foreach (var param in verb.UnnamedParams)
        {
            var val = ValueResolver.Resolve(param, context);
            if (sb.Length > 0) sb.Append(" ");

            if (val is ZohStr s) sb.Append(s.Value);
            else sb.Append(val.ToString());
        }

        var message = sb.ToString();
        var diagnostic = new Diagnostic(severity, "UserLog", message, verb.Start);

        // Error severity means execution failure?
        if (severity == DiagnosticSeverity.Error)
        {
            return VerbResult.Fatal(diagnostic);
        }

        return VerbResult.WithDiagnostics(ZohValue.Nothing, [diagnostic]);
    }
}
