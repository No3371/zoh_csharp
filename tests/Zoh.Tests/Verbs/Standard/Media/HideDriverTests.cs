using Xunit;
using Zoh.Runtime.Execution;
using Zoh.Runtime.Verbs.Standard.Media;
using System.Collections.Generic;

namespace Zoh.Tests.Verbs.Standard.Media;

public class HideDriverTests
{
    private class MockHideHandler : IHideHandler
    {
        public List<HideRequest> Requests { get; } = new();

        public void OnHide(IExecutionContext context, HideRequest request)
        {
            Requests.Add(request);
        }
    }

    private ZohRuntime CreateRuntimeWithMockHandler(IHideHandler handler)
    {
        var runtime = new ZohRuntime();
        runtime.Handlers.RegisterCoreHandlers();
        runtime.VerbRegistry.Register(new HideDriver(handler));
        return runtime;
    }

    [Fact]
    public void Hide_Basic_CallsHandler()
    {
        var handler = new MockHideHandler();
        var runtime = CreateRuntimeWithMockHandler(handler);

        var story = runtime.LoadStory(@"
        @start
        /hide ""bg-layer"";
        ");

        var context = runtime.CreateContext(story);
        runtime.Run(context, story);

        Assert.Equal(ContextState.Terminated, context.State);
        var req = Assert.Single(handler.Requests);
        Assert.Equal("bg-layer", req.Id);
        Assert.Equal(0, req.FadeDuration);
        Assert.Equal("linear", req.Easing);
    }

    [Fact]
    public void Hide_WithAttributes_ParsesAll()
    {
        var handler = new MockHideHandler();
        var runtime = CreateRuntimeWithMockHandler(handler);

        var story = runtime.LoadStory(@"
        @start
        /hide [Fade:2.5] [Easing:""ease-out""] ""overlay"";
        ");

        var context = runtime.CreateContext(story);
        runtime.Run(context, story);

        var req = Assert.Single(handler.Requests);
        Assert.Equal("overlay", req.Id);
        Assert.Equal(2.5, req.FadeDuration);
        Assert.Equal("ease-out", req.Easing);
    }

    [Fact]
    public void Hide_MissingId_ThrowsCompilationException()
    {
        var runtime = new ZohRuntime();
        runtime.Handlers.RegisterCoreHandlers();

        Assert.Throws<CompilationException>(() => runtime.LoadStory(@"
        @start
        /hide;
        "));
    }
}
