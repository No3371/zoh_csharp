using Xunit;
using Zoh.Runtime.Execution;
using Zoh.Runtime.Types;
using Zoh.Runtime.Diagnostics;

namespace Zoh.Tests.Verbs.Channel;

public class ChannelVerbTimeoutTests
{
    private ZohRuntime CreateRuntime()
    {
        var runtime = new ZohRuntime();
        runtime.Handlers.RegisterCoreHandlers();
        return runtime;
    }

    [Fact]
    public void Pull_TimeoutZero_ReturnsInfoDiagnosticImmediately()
    {
        var runtime = CreateRuntime();
        var story = runtime.LoadStory(@"
            @start
            /open <ch>;
            /pull <ch>, timeout:0;
        ");
        var ctx = runtime.CreateContext(story);
        runtime.Run(ctx);

        var internalCtx = (Context)ctx;
        Assert.Equal(ContextState.Terminated, ctx.State);
        Assert.Equal(ZohValue.Nothing, internalCtx.LastResult);
        Assert.Contains(internalCtx.LastDiagnostics, d => d.Severity == DiagnosticSeverity.Info && d.Code == "timeout");
    }

    [Fact]
    public void Pull_TimeoutNegative_ReturnsInfoDiagnosticImmediately()
    {
        var runtime = CreateRuntime();
        var story = runtime.LoadStory(@"
            @start
            /open <ch>;
            /pull <ch>, timeout:-1;
        ");
        var ctx = runtime.CreateContext(story);
        runtime.Run(ctx);

        var internalCtx = (Context)ctx;
        Assert.Equal(ContextState.Terminated, ctx.State);
        Assert.Contains(internalCtx.LastDiagnostics, d => d.Severity == DiagnosticSeverity.Info && d.Code == "timeout");
    }

    [Fact]
    public void Pull_TimeoutQuestion_ProceedsNormally()
    {
        var runtime = CreateRuntime();
        var story = runtime.LoadStory(@"
            @start
            /open <ch>;
            /push <ch>, 42;
            /pull <ch>, timeout:?;
        ");
        var ctx = runtime.CreateContext(story);
        runtime.Run(ctx);

        var internalCtx = (Context)ctx;
        Assert.Equal(ContextState.Terminated, ctx.State);
        Assert.DoesNotContain(internalCtx.LastDiagnostics, d => d.Code == "timeout");
        Assert.Equal(new ZohInt(42), internalCtx.LastResult);
    }

    [Fact]
    public void Push_TimeoutZero_ReturnsInfoDiagnosticImmediately()
    {
        var runtime = CreateRuntime();
        var story = runtime.LoadStory(@"
            @start
            /open <ch>;
            /push <ch>, 99, timeout:0;
        ");
        var ctx = runtime.CreateContext(story);
        runtime.Run(ctx);

        var internalCtx = (Context)ctx;
        Assert.Equal(ContextState.Terminated, ctx.State);
        Assert.Equal(ZohValue.Nothing, internalCtx.LastResult);
        Assert.Contains(internalCtx.LastDiagnostics, d => d.Severity == DiagnosticSeverity.Info && d.Code == "timeout");
    }

    [Fact]
    public void Push_TimeoutNegative_ReturnsInfoDiagnosticImmediately()
    {
        var runtime = CreateRuntime();
        var story = runtime.LoadStory(@"
            @start
            /open <ch>;
            /push <ch>, 99, timeout:-1;
        ");
        var ctx = runtime.CreateContext(story);
        runtime.Run(ctx);

        var internalCtx = (Context)ctx;
        Assert.Equal(ContextState.Terminated, ctx.State);
        Assert.Contains(internalCtx.LastDiagnostics, d => d.Severity == DiagnosticSeverity.Info && d.Code == "timeout");
    }

    [Fact]
    public void Push_TimeoutQuestion_ProceedsNormally()
    {
        var runtime = CreateRuntime();
        var story = runtime.LoadStory(@"
            @start
            /open <ch>;
            /push <ch>, 99, timeout:?;
            /pull <ch>;
        ");
        var ctx = runtime.CreateContext(story);
        runtime.Run(ctx);

        var internalCtx = (Context)ctx;
        Assert.Equal(ContextState.Terminated, ctx.State);
        Assert.DoesNotContain(internalCtx.LastDiagnostics, d => d.Code == "timeout");
        Assert.Equal(new ZohInt(99), internalCtx.LastResult);
    }
}
