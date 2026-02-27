using Xunit;
using Zoh.Runtime.Execution;
using Zoh.Runtime.Verbs.Standard.Media;
using System.Collections.Generic;

namespace Zoh.Tests.Verbs.Standard.Media;

public class StopDriverTests
{
    private class MockStopHandler : IStopHandler
    {
        public List<StopRequest> Requests { get; } = new();

        public void OnStop(IExecutionContext context, StopRequest request)
        {
            Requests.Add(request);
        }
    }

    private ZohRuntime CreateRuntimeWithMockHandler(IStopHandler handler)
    {
        var runtime = new ZohRuntime();
        runtime.Handlers.RegisterCoreHandlers();
        runtime.VerbRegistry.Register(new StopDriver(handler));
        return runtime;
    }

    [Fact]
    public void Stop_WithId_CallsHandler()
    {
        var handler = new MockStopHandler();
        var runtime = CreateRuntimeWithMockHandler(handler);

        var story = runtime.LoadStory(@"
        @start
        /stop ""bgm"";
        ");

        var context = runtime.CreateContext(story);
        runtime.Run(context);

        Assert.Equal(ContextState.Terminated, context.State);
        var req = Assert.Single(handler.Requests);
        Assert.Equal("bgm", req.Id);
        Assert.Equal(0, req.FadeDuration);
        Assert.Equal("linear", req.Easing);
    }

    [Fact]
    public void Stop_NoId_StopsAll()
    {
        var handler = new MockStopHandler();
        var runtime = CreateRuntimeWithMockHandler(handler);

        var story = runtime.LoadStory(@"
        @start
        /stop;
        ");

        var context = runtime.CreateContext(story);
        runtime.Run(context);

        var req = Assert.Single(handler.Requests);
        Assert.Null(req.Id);
        Assert.Equal(0, req.FadeDuration);
    }

    [Fact]
    public void Stop_WithFade_ParsesAttributes()
    {
        var handler = new MockStopHandler();
        var runtime = CreateRuntimeWithMockHandler(handler);

        var story = runtime.LoadStory(@"
        @start
        /stop [Fade:1.5] [Easing:""ease-in-out""] ""ambient"";
        ");

        var context = runtime.CreateContext(story);
        runtime.Run(context);

        var req = Assert.Single(handler.Requests);
        Assert.Equal("ambient", req.Id);
        Assert.Equal(1.5, req.FadeDuration);
        Assert.Equal("ease-in-out", req.Easing);
    }
}
