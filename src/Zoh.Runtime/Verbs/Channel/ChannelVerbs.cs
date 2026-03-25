using System;
using System.Collections.Immutable;
using Zoh.Runtime.Execution;
using Zoh.Runtime.Types;
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
        if (call.UnnamedParams.Length < 2)
            return DriverResult.Complete.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "parameter_not_found", "Expected channel and value parameters", call.Start));

        var channelValue = Zoh.Runtime.Execution.ValueResolver.Resolve(call.UnnamedParams[0], context);
        if (channelValue is not ZohChannel channel)
            return DriverResult.Complete.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "invalid_type", $"Expected channel, got: {channelValue.Type}", call.Start));

        var value = Zoh.Runtime.Execution.ValueResolver.Resolve(call.UnnamedParams[1], context);

        double? timeoutMs = null;
        foreach (var param in call.NamedParams)
        {
            if (param.Key.Equals("timeout", StringComparison.OrdinalIgnoreCase))
            {
                var tVal = Zoh.Runtime.Execution.ValueResolver.Resolve(param.Value, context);
                if (tVal is ZohFloat f)
                {
                    if (f.Value <= 0)
                        return new DriverResult.Complete(ZohValue.Nothing, ImmutableArray.Create(
                            new Diagnostic(DiagnosticSeverity.Info, "timeout", "The timeout was reached.", call.Start)));
                    timeoutMs = f.Value * 1000.0;
                }
                else if (tVal is ZohInt i)
                {
                    if (i.Value <= 0)
                        return new DriverResult.Complete(ZohValue.Nothing, ImmutableArray.Create(
                            new Diagnostic(DiagnosticSeverity.Info, "timeout", "The timeout was reached.", call.Start)));
                    timeoutMs = i.Value * 1000.0;
                }
                break;
            }
        }

        if (!context.ChannelManager.Exists(channel.Name))
            return DriverResult.Complete.Error(ZohValue.Nothing, new Diagnostic(DiagnosticSeverity.Error, "not_found", $"Channel does not exist: {channel.Name}", call.Start));

        if (!context.ChannelManager.TryPush(channel.Name, value))
            return DriverResult.Complete.Error(ZohValue.Nothing, new Diagnostic(DiagnosticSeverity.Error, "closed", $"Cannot push to closed channel: {channel.Name}", call.Start));

        return DriverResult.Complete.Ok(ZohValue.Nothing);
    }
}

public class PullVerbDriver : IVerbDriver
{
    public string Namespace => "core.channel";
    public string Name => "pull";

    public DriverResult Execute(IExecutionContext context, VerbCallAst call)
    {
        if (call.UnnamedParams.Length < 1)
            return DriverResult.Complete.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "parameter_not_found", "Expected channel parameter", call.Start));

        var channelValue = Zoh.Runtime.Execution.ValueResolver.Resolve(call.UnnamedParams[0], context);
        if (channelValue is not ZohChannel channel)
            return DriverResult.Complete.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "invalid_type", $"Expected channel, got: {channelValue.Type}", call.Start));

        // Get current generation for comparison
        var currentGen = context.ChannelManager.GetGeneration(channel.Name);
        if (currentGen == 0)
            return DriverResult.Complete.Error(ZohValue.Nothing, new Diagnostic(DiagnosticSeverity.Error, "not_found", $"Channel does not exist: {channel.Name}", call.Start));

        double? timeoutMs = null;
        foreach (var param in call.NamedParams)
        {
            if (param.Key.Equals("timeout", StringComparison.OrdinalIgnoreCase))
            {
                var tVal = Zoh.Runtime.Execution.ValueResolver.Resolve(param.Value, context);
                if (tVal is ZohFloat f)
                {
                    if (f.Value <= 0)
                        return new DriverResult.Complete(ZohValue.Nothing, ImmutableArray.Create(
                            new Diagnostic(DiagnosticSeverity.Info, "timeout", "The timeout was reached.", call.Start)));
                    timeoutMs = f.Value * 1000.0;
                }
                else if (tVal is ZohInt i)
                {
                    if (i.Value <= 0)
                        return new DriverResult.Complete(ZohValue.Nothing, ImmutableArray.Create(
                            new Diagnostic(DiagnosticSeverity.Info, "timeout", "The timeout was reached.", call.Start)));
                    timeoutMs = i.Value * 1000.0;
                }
                break;
            }
        }

        // For now: non-blocking pull. Blocking requires async redesign.
        var result = context.ChannelManager.TryPull(channel.Name, currentGen);

        return result.Status switch
        {
            PullStatus.Success => DriverResult.Complete.Ok(result.Value!),
            PullStatus.NotFound => DriverResult.Complete.Error(ZohValue.Nothing, new Diagnostic(DiagnosticSeverity.Error, "not_found", $"Channel does not exist: {channel.Name}", call.Start)),
            PullStatus.Closed => DriverResult.Complete.Error(ZohValue.Nothing, new Diagnostic(DiagnosticSeverity.Error, "closed", $"Channel is closed: {channel.Name}", call.Start)),
            PullStatus.GenerationMismatch => DriverResult.Complete.Error(ZohValue.Nothing, new Diagnostic(DiagnosticSeverity.Error, "stale", $"Channel was recreated: {channel.Name}", call.Start)),
            PullStatus.Empty => DriverResult.Complete.Ok(ZohValue.Nothing), // Non-blocking: return nothing if empty
            _ => DriverResult.Complete.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "internal", "Unknown pull status", call.Start))
        };
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
