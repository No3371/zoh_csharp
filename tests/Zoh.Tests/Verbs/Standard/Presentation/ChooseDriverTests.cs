using Xunit;
using Zoh.Runtime.Execution;
using Zoh.Runtime.Verbs.Standard.Presentation;
using Zoh.Runtime.Types;
using System.Collections.Generic;
using System.Linq;

namespace Zoh.Tests.Verbs.Standard.Presentation;

public class ChooseDriverTests
{
    private class MockChooseHandler : IChooseHandler
    {
        public List<ChooseRequest> Requests { get; } = new();

        public void OnChoose(ContextHandle handle, ChooseRequest request)
        {
            Requests.Add(request);
        }
    }

    private ZohRuntime CreateRuntimeWithChoose(IChooseHandler handler)
    {
        var runtime = new ZohRuntime();
        runtime.Handlers.RegisterCoreHandlers();

        var driver = new ChooseDriver(handler);
        runtime.VerbRegistry.Register(driver);

        return runtime;
    }

    [Fact]
    public void Choose_BasicWait_YieldsHostContinuation()
    {
        var handler = new MockChooseHandler();
        var runtime = CreateRuntimeWithChoose(handler);

        CompiledStory story;
        try
        {
            story = runtime.LoadStory(@"
            @start
            /choose prompt:""Where to??"",
                true, ""North"", ""n"",
                true, ""South"", ""s"";
            ");
        }
        catch (CompilationException ex)
        {
            var msg = string.Join("\n", ex.Diagnostics.Select(d => d.Message));
            throw new System.Exception("Compilation failed: " + msg, ex);
        }

        var ctx = runtime.CreateContext(story);
        runtime.Run(ctx);

        Assert.Equal(ContextState.WaitingHost, ctx.State);
        var request = handler.Requests.Single();
        Assert.Equal("Where to??", request.Prompt);
        Assert.Null(request.Tag);
        Assert.Equal(2, request.Choices.Count);
        Assert.Equal("North", request.Choices[0].Text);
        Assert.Equal("n", ((ZohStr)request.Choices[0].Value).Value);
    }

    [Fact]
    public void Choose_WithTagAttribute_PassesTagToHandler()
    {
        var handler = new MockChooseHandler();
        var runtime = CreateRuntimeWithChoose(handler);

        CompiledStory story;
        try
        {
            story = runtime.LoadStory(@"
            @start
            /choose [tag:""nav""] prompt:""Where to??"",
                true, ""North"", ""n"";
            ");
        }
        catch (CompilationException ex)
        {
            var msg = string.Join("\n", ex.Diagnostics.Select(d => d.Message));
            throw new System.Exception("Compilation failed: " + msg, ex);
        }

        var ctx = runtime.CreateContext(story);
        runtime.Run(ctx);

        Assert.Equal(ContextState.WaitingHost, ctx.State);
        var request = handler.Requests.Single();
        Assert.Equal("nav", request.Tag);
    }

    [Fact]
    public void Choose_ConditionHidesChoice()
    {
        var handler = new MockChooseHandler();
        var runtime = CreateRuntimeWithChoose(handler);

        CompiledStory story;
        try
        {
            story = runtime.LoadStory(@"
            @start
            /choose
                true, ""Always"", 1,
                false, ""Never"", 2;
            ");
        }
        catch (CompilationException ex)
        {
            var msg = string.Join("\n", ex.Diagnostics.Select(d => d.Message));
            throw new System.Exception("Compilation failed: " + msg, ex);
        }

        var ctx = runtime.CreateContext(story);
        runtime.Run(ctx);

        var request = handler.Requests.Single();
        Assert.Single(request.Choices);
        Assert.Equal("Always", request.Choices[0].Text);
        Assert.Equal(1L, ((ZohInt)request.Choices[0].Value).Value);
    }

    [Fact]
    public void Choose_AttributesParsed()
    {
        var handler = new MockChooseHandler();
        var runtime = CreateRuntimeWithChoose(handler);

        CompiledStory story;
        try
        {
            story = runtime.LoadStory(@"
            @start
            /choose [By:""System""] [Portrait:""Alert""] [Style:""Box""] timeout:10, true, ""OK"", 1;
            ");
        }
        catch (CompilationException ex)
        {
            var msg = string.Join("\n", ex.Diagnostics.Select(d => d.Message));
            throw new System.Exception("Compilation failed: " + msg, ex);
        }

        var ctx = runtime.CreateContext(story);
        runtime.Run(ctx);

        var request = handler.Requests.Single();
        Assert.Equal("System", request.Speaker);
        Assert.Equal("Alert", request.Portrait);
        Assert.Equal("Box", request.Style);
        Assert.Equal(10000.0, request.TimeoutMs);
    }
}
