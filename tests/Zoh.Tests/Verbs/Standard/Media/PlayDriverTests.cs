using Xunit;
using Zoh.Runtime.Execution;
using Zoh.Runtime.Verbs.Standard.Media;
using Zoh.Runtime.Types;
using System.Collections.Generic;

namespace Zoh.Tests.Verbs.Standard.Media;

public class PlayDriverTests
{
    private class MockPlayHandler : IPlayHandler
    {
        public List<PlayRequest> Requests { get; } = new();

        public void OnPlay(IExecutionContext context, PlayRequest request)
        {
            Requests.Add(request);
        }
    }

    private ZohRuntime CreateRuntimeWithMockHandler(IPlayHandler handler)
    {
        var runtime = new ZohRuntime();
        runtime.Handlers.RegisterCoreHandlers();
        runtime.VerbRegistry.Register(new PlayDriver(handler));
        return runtime;
    }

    [Fact]
    public void Play_BasicResource_CallsHandlerWithDefaults()
    {
        var handler = new MockPlayHandler();
        var runtime = CreateRuntimeWithMockHandler(handler);

        var story = runtime.LoadStory(@"
        @start
        /play ""bgm.mp3"";
        ");

        var context = runtime.CreateContext(story);
        runtime.Run(context);

        Assert.Equal(ContextState.Terminated, context.State);
        var req = Assert.Single(handler.Requests);
        Assert.Equal("bgm.mp3", req.Resource);
        Assert.Equal("bgm.mp3", req.Id);
        Assert.Equal(1.0, req.Volume);
        Assert.Equal(1, req.Loops);
        Assert.Equal("linear", req.Easing);
    }

    [Fact]
    public void Play_WithAttributes_ParsesAll()
    {
        var handler = new MockPlayHandler();
        var runtime = CreateRuntimeWithMockHandler(handler);

        var story = runtime.LoadStory(@"
        @start
        /play [Id:""ambient""] [Volume:0.8] [Loops:-1] [Easing:""sine""] ""wind.ogg"";
        ");

        var context = runtime.CreateContext(story);
        runtime.Run(context);

        var req = Assert.Single(handler.Requests);
        Assert.Equal("wind.ogg", req.Resource);
        Assert.Equal("ambient", req.Id);
        Assert.Equal(0.8, req.Volume);
        Assert.Equal(-1, req.Loops);
        Assert.Equal("sine", req.Easing);
    }

    [Fact]
    public void Play_ReturnsId()
    {
        var handler = new MockPlayHandler();
        var runtime = CreateRuntimeWithMockHandler(handler);

        var story = runtime.LoadStory(@"
        @start
        /play [Id:""track_1""] ""song.mp3""; -> *result;
        ");

        var context = runtime.CreateContext(story);
        runtime.Run(context);

        var ctx = (Context)context;
        var result = ctx.Variables.Get("result");
        Assert.IsType<ZohStr>(result);
        Assert.Equal("track_1", ((ZohStr)result).Value);
    }

    [Fact]
    public void Play_MissingResource_ThrowsCompilationException()
    {
        var runtime = new ZohRuntime();
        runtime.Handlers.RegisterCoreHandlers();

        Assert.Throws<CompilationException>(() => runtime.LoadStory(@"
        @start
        /play;
        "));
    }
}
