using Xunit;
using Zoh.Runtime.Execution;
using Zoh.Runtime.Types;
using Zoh.Runtime.Diagnostics;
using Zoh.Runtime.Verbs;

namespace Zoh.Tests.Verbs.Signals;

public class WaitDriverTests
{
    private ZohRuntime CreateRuntime()
    {
        var runtime = new ZohRuntime();
        runtime.Handlers.RegisterCoreHandlers();
        return runtime;
    }

    [Fact]
    public void Wait_TimeoutZero_ReturnsInfoDiagnosticImmediately()
    {
        var runtime = CreateRuntime();
        var story = runtime.LoadStory(@"
            @start
            /wait ""sig"", timeout:0;
        ");
        var ctx = runtime.CreateContext(story);
        runtime.Run(ctx);

        var internalCtx = (Context)ctx;
        Assert.Equal(ContextState.Terminated, ctx.State);
        Assert.Equal(ZohValue.Nothing, internalCtx.LastResult);
        Assert.Contains(internalCtx.LastDiagnostics, d => d.Severity == DiagnosticSeverity.Info && d.Code == "timeout");
    }

    [Fact]
    public void Wait_TimeoutNegative_ReturnsInfoDiagnosticImmediately()
    {
        var runtime = CreateRuntime();
        var story = runtime.LoadStory(@"
            @start
            /wait ""sig"", timeout:-1;
        ");
        var ctx = runtime.CreateContext(story);
        runtime.Run(ctx);

        var internalCtx = (Context)ctx;
        Assert.Equal(ContextState.Terminated, ctx.State);
        Assert.Contains(internalCtx.LastDiagnostics, d => d.Severity == DiagnosticSeverity.Info && d.Code == "timeout");
    }

    [Fact]
    public void Wait_TimeoutQuestion_SuspendsNormally()
    {
        var runtime = CreateRuntime();
        var story = runtime.LoadStory(@"
            @start
            /wait ""sig"", timeout:?;
        ");
        var ctx = runtime.CreateContext(story);
        runtime.Run(ctx);

        Assert.Equal(ContextState.WaitingMessage, ctx.State);
    }

    [Fact]
    public void Wait_NoTimeout_SuspendsOnSignal()
    {
        var runtime = CreateRuntime();
        var story = runtime.LoadStory(@"
            @start
            /wait ""sig"";
        ");
        var ctx = runtime.CreateContext(story);
        runtime.Run(ctx);

        Assert.Equal(ContextState.WaitingMessage, ctx.State);
    }

    [Fact]
    public void Wait_ResumeTimedOut_ReturnsInfoDiagnostic()
    {
        var runtime = CreateRuntime();
        var story = runtime.LoadStory(@"
            @start
            /wait ""sig"";
        ");
        var ctx = runtime.CreateContext(story);
        runtime.Run(ctx);

        Assert.Equal(ContextState.WaitingMessage, ctx.State);

        var internalCtx = (Context)ctx;
        internalCtx.Resume(new WaitTimedOut(), internalCtx.ResumeToken);
        runtime.Run(ctx);

        Assert.Equal(ContextState.Terminated, ctx.State);
        Assert.Equal(ZohValue.Nothing, internalCtx.LastResult);
        Assert.Contains(internalCtx.LastDiagnostics, d => d.Severity == DiagnosticSeverity.Info && d.Code == "timeout");
    }

    [Fact]
    public void Wait_ResumeWithValue_ReturnsValue()
    {
        var runtime = CreateRuntime();
        var story = runtime.LoadStory(@"
            @start
            /wait ""sig"";
        ");
        var ctx = runtime.CreateContext(story);
        runtime.Run(ctx);

        var internalCtx = (Context)ctx;
        var payload = new ZohInt(42);
        internalCtx.Resume(new WaitCompleted(payload), internalCtx.ResumeToken);
        runtime.Run(ctx);

        Assert.Equal(ContextState.Terminated, ctx.State);
        Assert.Equal(payload, internalCtx.LastResult);
        Assert.DoesNotContain(internalCtx.LastDiagnostics, d => d.Code == "timeout");
    }
}
