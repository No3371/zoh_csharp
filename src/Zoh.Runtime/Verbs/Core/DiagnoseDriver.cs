using System.Collections.Immutable;
using Zoh.Runtime.Execution;
using Zoh.Runtime.Parsing.Ast;
using Zoh.Runtime.Types;
using Zoh.Runtime.Diagnostics;

namespace Zoh.Runtime.Verbs.Core;

public class DiagnoseDriver : IVerbDriver
{
    public string Namespace => "core";
    public string Name => "diagnose";

    public DriverResult Execute(IExecutionContext context, VerbCallAst verb)
    {
        // /diagnose;

        var groups = context.LastDiagnostics
            .GroupBy(d => d.Severity switch
            {
                DiagnosticSeverity.Fatal => "fatal",
                DiagnosticSeverity.Error => "error",
                DiagnosticSeverity.Warning => "warning",
                DiagnosticSeverity.Info => "info",
                _ => "unknown"
            })
            .ToDictionary(g => g.Key, g => (ZohValue)new ZohList(g.Select(d => (ZohValue)new ZohStr(d.Message)).ToImmutableArray()));

        // Ensure result has keys for severity levels present in diagnostics

        var map = new ZohMap(groups.ToImmutableDictionary());

        // Spec says: "returns a Nothing" if no diagnostics. 
        // Or "map of following structure... or Nothing if no diagnostics are returned."

        if (context.LastDiagnostics.Count == 0)
        {
            return DriverResult.Complete.Ok(ZohValue.Nothing);
        }

        return DriverResult.Complete.Ok(map);
    }
}
