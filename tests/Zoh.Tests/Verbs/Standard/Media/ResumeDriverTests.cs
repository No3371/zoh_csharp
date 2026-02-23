using Xunit;
using Zoh.Runtime.Execution;
using Zoh.Runtime.Verbs.Standard.Media;
using System.Collections.Generic;

namespace Zoh.Tests.Verbs.Standard.Media;

public class ResumeDriverTests
{
    private class MockResumeHandler : IResumeHandler
    {
        public List<ResumeRequest> Requests { get; } = new();

        public void OnResume(IExecutionContext context, ResumeRequest request)
        {
            Requests.Add(request);
        }
    }

    private ZohRuntime CreateRuntimeWithMockHandler(IResumeHandler handler)
    {
        var runtime = new ZohRuntime();
        runtime.Handlers.RegisterCoreHandlers();
        runtime.VerbRegistry.Register(new ResumeDriver(handler));
        return runtime;
    }

    [Fact]
    public void Resume_Basic_CallsHandler()
    {
        var handler = new MockResumeHandler();
        var runtime = CreateRuntimeWithMockHandler(handler);

        var story = runtime.LoadStory(@"
        @start
        /resume ""music"";
        ");

        var context = runtime.CreateContext(story);
        runtime.Run(context, story);

        Assert.Equal(ContextState.Terminated, context.State);
        var req = Assert.Single(handler.Requests);
        Assert.Equal("music", req.Id);
    }

    [Fact]
    public void Resume_MissingId_ThrowsCompilationException()
    {
        var runtime = new ZohRuntime();
        runtime.Handlers.RegisterCoreHandlers();

        Assert.Throws<CompilationException>(() => runtime.LoadStory(@"
        @start
        /resume;
        "));
    }
}
