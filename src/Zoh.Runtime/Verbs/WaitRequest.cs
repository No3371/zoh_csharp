using Zoh.Runtime.Execution;
using Zoh.Runtime.Types;

namespace Zoh.Runtime.Verbs;

public abstract record WaitRequest;
public sealed record SleepRequest(double DurationMs) : WaitRequest;
public sealed record SignalRequest(string MessageName, double? TimeoutMs = null) : WaitRequest;
public sealed record JoinContextRequest(ContextHandle Handle) : WaitRequest;
public sealed record HostRequest(double? TimeoutMs = null) : WaitRequest;
public sealed record ChannelPullRequest(string ChannelName, int Generation, double? TimeoutMs = null) : WaitRequest;
public sealed record ChannelPushRequest(string ChannelName, int Generation, double? TimeoutMs, ZohValue Value) : WaitRequest;
