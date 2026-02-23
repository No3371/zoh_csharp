using Xunit;
using Zoh.Runtime.Execution;
using Zoh.Runtime.Verbs.Standard.Media;
using System.Collections.Generic;

namespace Zoh.Tests.Verbs.Standard.Media;

public class SetVolumeDriverTests
{
    private class MockSetVolumeHandler : ISetVolumeHandler
    {
        public List<SetVolumeRequest> Requests { get; } = new();

        public void OnSetVolume(IExecutionContext context, SetVolumeRequest request)
        {
            Requests.Add(request);
        }
    }

    private ZohRuntime CreateRuntimeWithMockHandler(ISetVolumeHandler handler)
    {
        var runtime = new ZohRuntime();
        runtime.Handlers.RegisterCoreHandlers();
        runtime.VerbRegistry.Register(new SetVolumeDriver(handler));
        return runtime;
    }

    [Fact]
    public void SetVolume_Basic_CallsHandlerWithDefaults()
    {
        var handler = new MockSetVolumeHandler();
        var runtime = CreateRuntimeWithMockHandler(handler);

        var story = runtime.LoadStory(@"
        @start
        /setVolume ""bgm"", 0.5;
        ");

        var context = runtime.CreateContext(story);
        runtime.Run(context, story);

        Assert.Equal(ContextState.Terminated, context.State);
        var req = Assert.Single(handler.Requests);
        Assert.Equal("bgm", req.Id);
        Assert.Equal(0.5, req.Volume);
        Assert.Equal(0, req.FadeDuration);
        Assert.Equal("linear", req.Easing);
    }

    [Fact]
    public void SetVolume_WithInt_CallsHandler()
    {
        var handler = new MockSetVolumeHandler();
        var runtime = CreateRuntimeWithMockHandler(handler);

        var story = runtime.LoadStory(@"
        @start
        /setVolume ""bgm"", 1;
        ");

        var context = runtime.CreateContext(story);
        runtime.Run(context, story);

        var req = Assert.Single(handler.Requests);
        Assert.Equal("bgm", req.Id);
        Assert.Equal(1.0, req.Volume);
    }

    [Fact]
    public void SetVolume_WithFade_ParsesAttributes()
    {
        var handler = new MockSetVolumeHandler();
        var runtime = CreateRuntimeWithMockHandler(handler);

        var story = runtime.LoadStory(@"
        @start
        /setVolume [Fade:2.0] [Easing:""ease-in""] ""ambient"", 0.3;
        ");

        var context = runtime.CreateContext(story);
        runtime.Run(context, story);

        var req = Assert.Single(handler.Requests);
        Assert.Equal("ambient", req.Id);
        Assert.Equal(0.3, req.Volume);
        Assert.Equal(2.0, req.FadeDuration);
        Assert.Equal("ease-in", req.Easing);
    }

    [Fact]
    public void SetVolume_MissingVolume_ThrowsCompilationException()
    {
        var runtime = new ZohRuntime();
        runtime.Handlers.RegisterCoreHandlers();

        Assert.Throws<CompilationException>(() => runtime.LoadStory(@"
        @start
        /setVolume ""bgm"";
        "));
    }
}
