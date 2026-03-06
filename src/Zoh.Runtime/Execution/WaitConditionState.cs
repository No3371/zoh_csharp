namespace Zoh.Runtime.Execution;

/// <summary>
/// Typed wait condition stored on a blocked context.
/// Used by the tick-loop scheduler (ResolveWait) to determine
/// when a blocked context should resume.
/// </summary>
public abstract record WaitConditionState;

public sealed record SleepCondition(double WakeTimeMs) : WaitConditionState;

public sealed record HostWaitCondition(double StartTimeMs, double? TimeoutMs) : WaitConditionState
{
    public bool IsTimedOut(double elapsedMs) =>
        TimeoutMs.HasValue && elapsedMs >= StartTimeMs + TimeoutMs.Value;
}

public sealed record SignalWaitCondition(
    string MessageName, double StartTimeMs, double? TimeoutMs) : WaitConditionState
{
    public bool IsTimedOut(double elapsedMs) =>
        TimeoutMs.HasValue && elapsedMs >= StartTimeMs + TimeoutMs.Value;
}

public sealed record ContextJoinCondition(ContextHandle TargetHandle) : WaitConditionState;

public sealed record ChannelWaitCondition(
    string ChannelName, double StartTimeMs, double? TimeoutMs) : WaitConditionState
{
    public bool IsTimedOut(double elapsedMs) =>
        TimeoutMs.HasValue && elapsedMs >= StartTimeMs + TimeoutMs.Value;
}
