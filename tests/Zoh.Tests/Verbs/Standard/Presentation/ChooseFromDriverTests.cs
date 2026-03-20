using Xunit;
using Zoh.Runtime.Execution;
using Zoh.Runtime.Verbs.Standard.Presentation;
using Zoh.Runtime.Types;
using System.Collections.Generic;
using System.Linq;
using Zoh.Runtime.Diagnostics;
using Zoh.Runtime.Verbs;

namespace Zoh.Tests.Verbs.Standard.Presentation;

public class ChooseFromDriverTests
{
    private class MockChooseFromHandler : IChooseFromHandler
    {
        public List<ChooseRequest> Requests { get; } = new();

        public void OnChooseFrom(ContextHandle handle, ChooseRequest request)
        {
            Requests.Add(request);
        }
    }

    private ZohRuntime CreateRuntimeWithChooseFrom(IChooseFromHandler handler)
    {
        var runtime = new ZohRuntime();
        runtime.Handlers.RegisterCoreHandlers();

        var driver = new ChooseFromDriver(handler);
        runtime.VerbRegistry.Register(driver);

        return runtime;
    }

    [Fact]
    public void ChooseFrom_BasicList_YieldsHostContinuation()
    {
        var handler = new MockChooseFromHandler();
        var runtime = CreateRuntimeWithChooseFrom(handler);

        CompiledStory story;
        try
        {
            story = runtime.LoadStory(@"
            @start
            *myList <- [
                {""text"": ""North"", ""value"": ""n"", ""visible"": true},
                {""text"": ""South"", ""value"": ""s""}
            ];
            /chooseFrom prompt:""Where to??"", *myList;
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
        Assert.Equal(2, request.Choices.Count);
        Assert.Equal("North", request.Choices[0].Text);
        Assert.Equal("n", ((ZohStr)request.Choices[0].Value).Value);
        Assert.Equal("South", request.Choices[1].Text);
        Assert.Equal("s", ((ZohStr)request.Choices[1].Value).Value);
        Assert.Null(request.Tag);
    }

    [Fact]
    public void ChooseFrom_WithTagAttribute_PassesTagToHandler()
    {
        var handler = new MockChooseFromHandler();
        var runtime = CreateRuntimeWithChooseFrom(handler);

        CompiledStory story;
        try
        {
            story = runtime.LoadStory(@"
            @start
            *list <- [{""text"": ""OK"", ""value"": 1}];
            /chooseFrom [tag:""confirm""] prompt:""Continue?"", *list;
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
        Assert.Equal("confirm", request.Tag);
    }

    [Fact]
    public void ChooseFrom_ConditionHidesChoice()
    {
        var handler = new MockChooseFromHandler();
        var runtime = CreateRuntimeWithChooseFrom(handler);

        CompiledStory story;
        try
        {
            story = runtime.LoadStory(@"
            @start
             *myList <- [
                {""text"": ""Always"", ""value"": 1, ""visible"": true},
                {""text"": ""Never"", ""value"": 2, ""visible"": false}
            ];
            /chooseFrom *myList;
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
    public void ChooseFrom_AttributesParsed()
    {
        var handler = new MockChooseFromHandler();
        var runtime = CreateRuntimeWithChooseFrom(handler);

        CompiledStory story;
        try
        {
            story = runtime.LoadStory(@"
            @start
            *list <- [{""text"": ""OK"", ""value"": 1}];
            /chooseFrom [By:""System""] [Portrait:""Alert""] [Style:""Box""] timeout:10, *list;
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

    [Fact]
    public void ChooseFrom_EmptyChoices_ReturnsWarning()
    {
        var handler = new MockChooseFromHandler();
        var runtime = CreateRuntimeWithChooseFrom(handler);

        var story = runtime.LoadStory(@"
            @start
            *myList <- [];
            /chooseFrom *myList;
        ");
        
        var ctx = runtime.CreateContext(story);
        runtime.Run(ctx);

        var internalCtx = (Zoh.Runtime.Execution.Context)ctx;
        Assert.Equal(ContextState.Terminated, ctx.State);
        Assert.Empty(handler.Requests);
        Assert.Equal(ZohValue.Nothing, internalCtx.LastResult);
        Assert.Contains(internalCtx.LastDiagnostics, d => d.Severity == DiagnosticSeverity.Warning && d.Code == "invalid_params");
    }

    [Fact]
    public void ChooseFrom_ResumeTimedOut_ReturnsInfoNothing()
    {
        var handler = new MockChooseFromHandler();
        var runtime = CreateRuntimeWithChooseFrom(handler);

        var story = runtime.LoadStory(@"
            @start
            *myList <- [{""text"": ""Choice"", ""value"": 1}];
            /chooseFrom *myList;
        ");
        
        var ctx = runtime.CreateContext(story);
        runtime.Run(ctx);

        var internalCtx = (Zoh.Runtime.Execution.Context)ctx;
        internalCtx.Resume(new WaitTimedOut(), internalCtx.ResumeToken);
        runtime.Run(ctx);

        Assert.Equal(ContextState.Terminated, ctx.State);
        Assert.Contains(internalCtx.LastDiagnostics, d => d.Severity == DiagnosticSeverity.Info && d.Code == "timeout");
        Assert.Equal(ZohValue.Nothing, internalCtx.LastResult);
    }

    [Fact]
    public void ChooseFrom_ResumeCancelled_ReturnsError()
    {
        var handler = new MockChooseFromHandler();
        var runtime = CreateRuntimeWithChooseFrom(handler);

        var story = runtime.LoadStory(@"
            @start
            *myList <- [{""text"": ""Choice"", ""value"": 1}];
            /chooseFrom *myList;
        ");
        
        var ctx = runtime.CreateContext(story);
        runtime.Run(ctx);

        var internalCtx = (Zoh.Runtime.Execution.Context)ctx;
        internalCtx.Resume(new WaitCancelled("cancel_code", "Cancelled via API"), internalCtx.ResumeToken);
        runtime.Run(ctx);

        Assert.Equal(ContextState.Terminated, ctx.State);
        Assert.Contains(internalCtx.LastDiagnostics, d => d.Severity == DiagnosticSeverity.Error && d.Code == "cancel_code");
    }
}
