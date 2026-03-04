namespace Zoh.Runtime.Execution;

/// <summary>
/// Configuration for a ZOH runtime instance.
/// </summary>
public class RuntimeConfig
{
    /// <summary>Max concurrent contexts (fork limit). 0 = unlimited.</summary>
    public int MaxContexts { get; set; } = 0;

    /// <summary>Max channel buffer depth. 0 = unlimited.</summary>
    public int MaxChannelDepth { get; set; } = 0;

    /// <summary>Execution timeout in milliseconds. 0 = no timeout.</summary>
    public int ExecutionTimeoutMs { get; set; } = 0;

    /// <summary>Whether diagnostics are collected and accessible via /diagnose.</summary>
    public bool EnableDiagnostics { get; set; } = true;

    /// <summary>Statement budget per context.Run() invocation. 0 = unlimited.</summary>
    public int MaxStatementsPerTick { get; set; } = 0;

    /// <summary>Default RuntimeConfig with no limits.</summary>
    public static RuntimeConfig Default => new();
}
