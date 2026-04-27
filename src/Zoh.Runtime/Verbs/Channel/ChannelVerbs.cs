using System;
using System.Collections.Immutable;
using Zoh.Runtime.Execution;
using Zoh.Runtime.Types;
using Zoh.Runtime.Verbs;
using Zoh.Runtime.Diagnostics;
using Zoh.Runtime.Parsing.Ast;

namespace Zoh.Runtime.Verbs.Channel;

public class OpenVerbDriver : IVerbDriver
{
    public string Namespace => "core.channel";
    public string Name => "open";

    public DriverResult Execute(IExecutionContext context, VerbCallAst call)
    {
        if (call.UnnamedParams.Length < 1)
            return DriverResult.Complete.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "parameter_not_found", "Expected channel parameter", call.Start));

        var channelValue = Zoh.Runtime.Execution.ValueResolver.Resolve(call.UnnamedParams[0], context);
        if (channelValue is not ZohChannel channel)
            return DriverResult.Complete.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "invalid_type", $"Expected channel, got: {channelValue.Type}", call.Start));

        var generation = context.ChannelManager.Open(channel.Name);
        return DriverResult.Complete.Ok(ZohValue.Nothing);
    }
}

public class PushVerbDriver : IVerbDriver
{
    public string Namespace => "core.channel";
    public string Name => "push";

    public DriverResult Execute(IExecutionContext context, VerbCallAst call)
    {
        if (context is not Context ctx)
            return DriverResult.Complete.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "invalid_context", "Push requires a Context.", call.Start));

        if (call.UnnamedParams.Length < 2)
            return DriverResult.Complete.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "parameter_not_found", "Expected channel and value parameters", call.Start));

        var channelValue = Zoh.Runtime.Execution.ValueResolver.Resolve(call.UnnamedParams[0], ctx);
        if (channelValue is not ZohChannel channel)
            return DriverResult.Complete.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "invalid_type", $"Expected channel, got: {channelValue.Type}", call.Start));

        var value = Zoh.Runtime.Execution.ValueResolver.Resolve(call.UnnamedParams[1], ctx);

        var wait = true;
        double? timeoutMs = null;
        var immediateTimeout = false;
        foreach (var p in call.NamedParams)
        {
            if (p.Key.Equals("wait", StringComparison.OrdinalIgnoreCase))
            {
                var v = Zoh.Runtime.Execution.ValueResolver.Resolve(p.Value, ctx);
                if (v is ZohBool b)
                    wait = b.Value;
            }
            else if (p.Key.Equals("timeout", StringComparison.OrdinalIgnoreCase))
            {
                var v = Zoh.Runtime.Execution.ValueResolver.Resolve(p.Value, ctx);
                if (v is ZohFloat f)
                {
                    if (f.Value <= 0) immediateTimeout = true;
                    else timeoutMs = f.Value * 1000.0;
                }
                else if (v is ZohInt i)
                {
                    if (i.Value <= 0) immediateTimeout = true;
                    else timeoutMs = i.Value * 1000.0;
                }
            }
        }

        var gen = ctx.ChannelManager.GetGeneration(channel.Name);
        var offer = ctx.ChannelManager.OfferPush(channel.Name, gen, value, wait, ctx);
        switch (offer)
        {
            case OfferPushResult.NotFound:
                return DriverResult.Complete.Error(ZohValue.Nothing,
                    new Diagnostic(DiagnosticSeverity.Error, "not_found", $"Channel does not exist: {channel.Name}", call.Start));
            case OfferPushResult.Closed:
                return DriverResult.Complete.Error(ZohValue.Nothing,
                    new Diagnostic(DiagnosticSeverity.Error, "closed", $"Cannot push to closed channel: {channel.Name}", call.Start));
            case OfferPushResult.Delivered:
            case OfferPushResult.Buffered:
                return DriverResult.Complete.Ok(ZohValue.Nothing);
            case OfferPushResult.MustSuspend:
                if (immediateTimeout)
                    return new DriverResult.Complete(ZohValue.Nothing, ImmutableArray.Create(
                        new Diagnostic(DiagnosticSeverity.Info, "timeout", "The timeout was reached.", call.Start)));
                return new DriverResult.Suspend(new Continuation(
                    new ChannelPushRequest(channel.Name, gen, timeoutMs, value),
                    outcome => outcome switch
                    {
                        WaitCompleted => DriverResult.Complete.Ok(ZohValue.Nothing),
                        WaitTimedOut => new DriverResult.Complete(ZohValue.Nothing, ImmutableArray.Create(
                            new Diagnostic(DiagnosticSeverity.Info, "timeout", "The timeout was reached.", call.Start))),
                        WaitCancelled x => new DriverResult.Complete(ZohValue.Nothing, ImmutableArray.Create(
                            new Diagnostic(DiagnosticSeverity.Error, x.Code, x.Message, call.Start))),
                        _ => DriverResult.Complete.Ok(ZohValue.Nothing)
                    }));
            default:
                return DriverResult.Complete.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "internal", "Unknown OfferPushResult", call.Start));
        }
    }
}

public class PullVerbDriver : IVerbDriver
{
    public string Namespace => "core.channel";
    public string Name => "pull";

    public DriverResult Execute(IExecutionContext context, VerbCallAst call)
    {
        if (context is not Context ctx)
            return DriverResult.Complete.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "invalid_context", "Pull requires a Context.", call.Start));

        if (call.UnnamedParams.Length < 1)
            return DriverResult.Complete.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "parameter_not_found", "Expected channel parameter", call.Start));

        var channelValue = Zoh.Runtime.Execution.ValueResolver.Resolve(call.UnnamedParams[0], ctx);
        if (channelValue is not ZohChannel channel)
            return DriverResult.Complete.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "invalid_type", $"Expected channel, got: {channelValue.Type}", call.Start));

        double? timeoutMs = null;
        var immediateTimeout = false;
        foreach (var p in call.NamedParams)
        {
            if (p.Key.Equals("timeout", StringComparison.OrdinalIgnoreCase))
            {
                var v = Zoh.Runtime.Execution.ValueResolver.Resolve(p.Value, ctx);
                if (v is ZohFloat f)
                {
                    if (f.Value <= 0) immediateTimeout = true;
                    else timeoutMs = f.Value * 1000.0;
                }
                else if (v is ZohInt i)
                {
                    if (i.Value <= 0) immediateTimeout = true;
                    else timeoutMs = i.Value * 1000.0;
                }
            }
        }

        var gen = ctx.ChannelManager.GetGeneration(channel.Name);
        var attempt = ctx.ChannelManager.AttemptPull(channel.Name, gen);
        switch (attempt.Status)
        {
            case AttemptPullStatus.NotFound:
                return DriverResult.Complete.Error(ZohValue.Nothing,
                    new Diagnostic(DiagnosticSeverity.Error, "not_found", $"Channel does not exist: {channel.Name}", call.Start));
            case AttemptPullStatus.Closed:
                return DriverResult.Complete.Error(ZohValue.Nothing,
                    new Diagnostic(DiagnosticSeverity.Error, "closed", $"Channel is closed: {channel.Name}", call.Start));
            case AttemptPullStatus.Stale:
                return DriverResult.Complete.Error(ZohValue.Nothing,
                    new Diagnostic(DiagnosticSeverity.Error, "stale", $"Channel was recreated: {channel.Name}", call.Start));
            case AttemptPullStatus.Delivered:
                return DriverResult.Complete.Ok(attempt.Value!);
            case AttemptPullStatus.Empty:
                if (immediateTimeout)
                    return new DriverResult.Complete(ZohValue.Nothing, ImmutableArray.Create(
                        new Diagnostic(DiagnosticSeverity.Info, "timeout", "The timeout was reached.", call.Start)));
                return new DriverResult.Suspend(new Continuation(
                    new ChannelPullRequest(channel.Name, gen, timeoutMs),
                    outcome => outcome switch
                    {
                        WaitCompleted c => DriverResult.Complete.Ok(c.Value),
                        WaitTimedOut => new DriverResult.Complete(ZohValue.Nothing, ImmutableArray.Create(
                            new Diagnostic(DiagnosticSeverity.Info, "timeout", "The timeout was reached.", call.Start))),
                        WaitCancelled x => new DriverResult.Complete(ZohValue.Nothing, ImmutableArray.Create(
                            new Diagnostic(DiagnosticSeverity.Error, x.Code, x.Message, call.Start))),
                        _ => DriverResult.Complete.Ok(ZohValue.Nothing)
                    }));
            default:
                return DriverResult.Complete.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "internal", "Unknown AttemptPullStatus", call.Start));
        }
    }
}

public class CloseVerbDriver : IVerbDriver
{
    public string Namespace => "core.channel";
    public string Name => "close";

    public DriverResult Execute(IExecutionContext context, VerbCallAst call)
    {
        if (call.UnnamedParams.Length < 1)
            return DriverResult.Complete.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "parameter_not_found", "Expected channel parameter", call.Start));

        var channelValue = Zoh.Runtime.Execution.ValueResolver.Resolve(call.UnnamedParams[0], context);
        if (channelValue is not ZohChannel channel)
            return DriverResult.Complete.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "invalid_type", $"Expected channel, got: {channelValue.Type}", call.Start));

        if (!context.ChannelManager.TryClose(channel.Name))
            return DriverResult.Complete.Error(ZohValue.Nothing, new Diagnostic(DiagnosticSeverity.Error, "not_found", $"Channel does not exist or already closed: {channel.Name}", call.Start));

        return DriverResult.Complete.Ok(ZohValue.Nothing);
    }
}
