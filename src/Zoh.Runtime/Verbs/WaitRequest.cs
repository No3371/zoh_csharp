using Zoh.Runtime.Execution;

namespace Zoh.Runtime.Verbs;

public abstract record WaitRequest;
public sealed record SleepRequest(double DurationMs) : WaitRequest;
public sealed record SignalRequest(string MessageName, double? TimeoutMs = null) : WaitRequest;
public sealed record JoinContextRequest(ContextHandle Handle) : WaitRequest;
public sealed record HostRequest(double? TimeoutMs = null) : WaitRequest;
