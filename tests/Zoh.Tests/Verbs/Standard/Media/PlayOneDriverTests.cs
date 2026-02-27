using Xunit;
using Zoh.Runtime.Execution;
using Zoh.Runtime.Verbs.Standard.Media;
using Zoh.Runtime.Types;
using System.Collections.Generic;

namespace Zoh.Tests.Verbs.Standard.Media;

public class PlayOneDriverTests
{
    private class MockPlayOneHandler : IPlayOneHandler
    {
        public List<PlayOneRequest> Requests { get; } = new();

        public void OnPlayOne(IExecutionContext context, PlayOneRequest request)
        {
            Requests.Add(request);
        }
    }

    private ZohRuntime CreateRuntimeWithMockHandler(IPlayOneHandler handler)
    {
        var runtime = new ZohRuntime();
        runtime.Handlers.RegisterCoreHandlers();
        runtime.VerbRegistry.Register(new PlayOneDriver(handler));
        return runtime;
    }

    [Fact]
    public void PlayOne_Basic_CallsHandlerWithDefaults()
    {
        var handler = new MockPlayOneHandler();
        var runtime = CreateRuntimeWithMockHandler(handler);

        var story = runtime.LoadStory(@"
        @start
        /playOne ""sfx.mp3"";
        ");

        var context = runtime.CreateContext(story);
        runtime.Run(context);

        Assert.Equal(ContextState.Terminated, context.State);
        var req = Assert.Single(handler.Requests);
        Assert.Equal("sfx.mp3", req.Resource);
        Assert.Equal(1.0, req.Volume);
        Assert.Equal(1, req.Loops);
    }

    [Fact]
    public void PlayOne_WithAttributes_ParsesAll()
    {
        var handler = new MockPlayOneHandler();
        var runtime = CreateRuntimeWithMockHandler(handler);

        var story = runtime.LoadStory(@"
        @start
        /playOne [Volume:0.5] [Loops:3] ""echo.wav"";
        ");

        var context = runtime.CreateContext(story);
        runtime.Run(context);

        var req = Assert.Single(handler.Requests);
        Assert.Equal("echo.wav", req.Resource);
        Assert.Equal(0.5, req.Volume);
        Assert.Equal(3, req.Loops);
    }

    [Fact]
    public void PlayOne_ReturnsNothing()
    {
        var handler = new MockPlayOneHandler();
        var runtime = CreateRuntimeWithMockHandler(handler);

        var story = runtime.LoadStory(@"
        @start
        /playOne ""sfx.mp3""; -> *result;
        ");

        var context = runtime.CreateContext(story);
        runtime.Run(context);

        var ctx = (Context)context;
        var result = ctx.Variables.Get("result");
        Assert.IsType<ZohNothing>(result);
    }

    [Fact]
    public void PlayOne_MissingResource_ThrowsCompilationException()
    {
        var runtime = new ZohRuntime();
        runtime.Handlers.RegisterCoreHandlers();

        Assert.Throws<CompilationException>(() => runtime.LoadStory(@"
        @start
        /playOne;
        "));
    }
}
