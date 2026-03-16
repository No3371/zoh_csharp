using Xunit;
using Zoh.Runtime.Execution;
using Zoh.Runtime.Verbs.Standard.Media;
using Zoh.Runtime.Parsing.Ast;
using Zoh.Runtime.Types;
using System.Collections.Generic;

namespace Zoh.Tests.Verbs.Standard.Media;

public class ShowDriverTests
{
    private class MockShowHandler : IShowHandler
    {
        public List<ShowRequest> Requests { get; } = new();

        public void OnShow(IExecutionContext context, ShowRequest request)
        {
            Requests.Add(request);
        }
    }

    private ZohRuntime CreateRuntimeWithMockHandler(IShowHandler handler)
    {
        var runtime = new ZohRuntime();
        runtime.Handlers.RegisterCoreHandlers();
        runtime.VerbRegistry.Register(new ShowDriver(handler));
        return runtime;
    }

    [Fact]
    public void Show_BasicResource_CallsHandlerWithDefaults()
    {
        var handler = new MockShowHandler();
        var runtime = CreateRuntimeWithMockHandler(handler);

        var story = runtime.LoadStory(@"
        @start
        /show ""image.png"";
        ");

        var context = runtime.CreateContext(story);
        runtime.Run(context);

        Assert.Equal(ContextState.Terminated, context.State);
        var req = Assert.Single(handler.Requests);
        Assert.Equal("image.png", req.Resource);
        Assert.Equal("image.png", req.Id);
        Assert.Null(req.RelativeWidth);
        Assert.Null(req.RelativeHeight);
        Assert.Null(req.Width);
        Assert.Null(req.Height);
        Assert.Equal("center", req.Anchor);
        Assert.Equal(0.5, req.PosX);
        Assert.Equal(0.5, req.PosY);
        Assert.Equal(0, req.PosZ);
        Assert.Equal(0, req.FadeDuration);
        Assert.Equal(1.0, req.Opacity);
        Assert.Equal("linear", req.Easing);
        Assert.Null(req.Tag);
    }

    [Fact]
    public void Show_WithTagAttribute_PassesTagToHandler()
    {
        var handler = new MockShowHandler();
        var runtime = CreateRuntimeWithMockHandler(handler);

        var story = runtime.LoadStory(@"
        @start
        /show [tag:""intro""] ""image.png"";
        ");

        var context = runtime.CreateContext(story);
        runtime.Run(context);

        Assert.Equal(ContextState.Terminated, context.State);
        var req = Assert.Single(handler.Requests);
        Assert.Equal("intro", req.Tag);
    }

    [Fact]
    public void Show_WithAttributes_ParsesAll()
    {
        var handler = new MockShowHandler();
        var runtime = CreateRuntimeWithMockHandler(handler);

        var story = runtime.LoadStory(@"
        @start
        /show
            [Id:""bg""] [RW:0.8] [RH:0.6] [Width:800] [Height:600]
            [Anchor:""top-left""] [PosX:0.25] [PosY:0.75] [PosZ:-1]
            [Fade:1.5] [Opacity:0.8] [Easing:""ease-in""]
            ""scene2"";
        ");

        var context = runtime.CreateContext(story);
        runtime.Run(context);

        Assert.Equal(ContextState.Terminated, context.State);
        var req = Assert.Single(handler.Requests);
        Assert.Equal("scene2", req.Resource);
        Assert.Equal("bg", req.Id);
        Assert.Equal(0.8, req.RelativeWidth);
        Assert.Equal(0.6, req.RelativeHeight);
        Assert.Equal(800, req.Width);
        Assert.Equal(600, req.Height);
        Assert.Equal("top-left", req.Anchor);
        Assert.Equal(0.25, req.PosX);
        Assert.Equal(0.75, req.PosY);
        Assert.Equal(-1, req.PosZ);
        Assert.Equal(1.5, req.FadeDuration);
        Assert.Equal(0.8, req.Opacity);
        Assert.Equal("ease-in", req.Easing);
    }

    [Fact]
    public void Show_ReturnsId()
    {
        var handler = new MockShowHandler();
        var runtime = CreateRuntimeWithMockHandler(handler);

        var story = runtime.LoadStory(@"
        @start
        /show [Id:""my-id""] ""test.png""; -> *result;
        ");

        var context = runtime.CreateContext(story);
        runtime.Run(context);

        var ctx = (Context)context;
        var result = ctx.Variables.Get("result");
        Assert.IsType<ZohStr>(result);
        Assert.Equal("my-id", ((ZohStr)result).Value);
    }

    [Fact]
    public void Show_MissingResource_ThrowsCompilationException()
    {
        var runtime = new ZohRuntime();
        runtime.Handlers.RegisterCoreHandlers();

        Assert.Throws<CompilationException>(() => runtime.LoadStory(@"
        @start
        /show;
        "));
    }
}
