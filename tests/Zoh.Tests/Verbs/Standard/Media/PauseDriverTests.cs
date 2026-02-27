using Xunit;
using Zoh.Runtime.Execution;
using Zoh.Runtime.Verbs.Standard.Media;
using System.Collections.Generic;

namespace Zoh.Tests.Verbs.Standard.Media;

public class PauseDriverTests
{
    private class MockPauseHandler : IPauseHandler
    {
        public List<PauseRequest> Requests { get; } = new();

        public void OnPause(IExecutionContext context, PauseRequest request)
        {
            Requests.Add(request);
        }
    }

    private ZohRuntime CreateRuntimeWithMockHandler(IPauseHandler handler)
    {
        var runtime = new ZohRuntime();
        runtime.Handlers.RegisterCoreHandlers();
        runtime.VerbRegistry.Register(new PauseDriver(handler));
        return runtime;
    }

    [Fact]
    public void Pause_Basic_CallsHandler()
    {
        var handler = new MockPauseHandler();
        var runtime = CreateRuntimeWithMockHandler(handler);

        var story = runtime.LoadStory(@"
        @start
        /pause ""music"";
        ");

        var context = runtime.CreateContext(story);
        runtime.Run(context);

        Assert.Equal(ContextState.Terminated, context.State);
        var req = Assert.Single(handler.Requests);
        Assert.Equal("music", req.Id);
    }

    [Fact]
    public void Pause_MissingId_ThrowsCompilationException()
    {
        var runtime = new ZohRuntime();
        runtime.Handlers.RegisterCoreHandlers();

        Assert.Throws<CompilationException>(() => runtime.LoadStory(@"
        @start
        /pause;
        "));
    }
}
