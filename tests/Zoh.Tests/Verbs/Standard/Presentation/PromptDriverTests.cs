using Xunit;
using Zoh.Runtime.Execution;
using Zoh.Runtime.Verbs.Standard.Presentation;
using Zoh.Runtime.Types;
using System.Collections.Generic;
using System.Linq;
using Zoh.Runtime.Diagnostics;
using Zoh.Runtime.Verbs;

namespace Zoh.Tests.Verbs.Standard.Presentation;

public class PromptDriverTests
{
    private class MockPromptHandler : IPromptHandler
    {
        public List<PromptRequest> Requests { get; } = new();

        public void OnPrompt(ContextHandle handle, PromptRequest request)
        {
            Requests.Add(request);
        }
    }

    private ZohRuntime CreateRuntimeWithPrompt(IPromptHandler handler)
    {
        var runtime = new ZohRuntime();
        runtime.Handlers.RegisterCoreHandlers();

        var driver = new PromptDriver(handler);
        runtime.VerbRegistry.Register(driver);

        return runtime;
    }

    [Fact]
    public void Prompt_BasicWait_YieldsHostContinuation()
    {
        var handler = new MockPromptHandler();
        var runtime = CreateRuntimeWithPrompt(handler);

        CompiledStory story;
        try
        {
            story = runtime.LoadStory(@"
            @start
            /prompt ""What is your name?"";
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
        Assert.Equal("What is your name?", request.PromptText);
        Assert.Null(request.TimeoutMs);
        Assert.Equal("default", request.Style);
        Assert.Null(request.Tag);
    }

    [Fact]
    public void Prompt_WithTagAttribute_PassesTagToHandler()
    {
        var handler = new MockPromptHandler();
        var runtime = CreateRuntimeWithPrompt(handler);

        CompiledStory story;
        try
        {
            story = runtime.LoadStory(@"
            @start
            /prompt [tag:""ask-name""] ""What is your name?"";
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
        Assert.Equal("ask-name", request.Tag);
    }

    [Fact]
    public void Prompt_AttributesParsed()
    {
        var handler = new MockPromptHandler();
        var runtime = CreateRuntimeWithPrompt(handler);

        CompiledStory story;
        try
        {
            story = runtime.LoadStory(@"
            @start
            /prompt [Style:""Password""] timeout:15, ""Enter Secret"";
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
        Assert.Equal("Password", request.Style);
        Assert.Equal("Enter Secret", request.PromptText);
        Assert.Equal(15000.0, request.TimeoutMs);
    }

    [Fact]
    public void Prompt_TimeoutZeroOrNegative_CompletesFast()
    {
        var handler = new MockPromptHandler();
        var runtime = CreateRuntimeWithPrompt(handler);

        CompiledStory story;
        try
        {
            story = runtime.LoadStory(@"
            @start
            /prompt timeout:0, ""Fast"";
            ");
        }
        catch (CompilationException ex)
        {
            var msg = string.Join("\n", ex.Diagnostics.Select(d => d.Message));
            throw new System.Exception("Compilation failed: " + msg, ex);
        }

        var ctx = runtime.CreateContext(story);
        runtime.Run(ctx);

        // When timeout is <= 0, the driver should complete immediately with Nothing.
        var internalCtx = (Zoh.Runtime.Execution.Context)ctx;
        Assert.Equal(ContextState.Terminated, ctx.State);
        Assert.Empty(handler.Requests); // Should NOT even hit the handler
        Assert.Equal(ZohValue.Nothing, internalCtx.LastResult);
        Assert.Contains(internalCtx.LastDiagnostics, d => d.Severity == DiagnosticSeverity.Info && d.Code == "timeout");
    }

    [Fact]
    public void Prompt_ResumeTimedOut_ReturnsInfoNothing()
    {
        var handler = new MockPromptHandler();
        var runtime = CreateRuntimeWithPrompt(handler);

        var story = runtime.LoadStory(@"
            @start
            /prompt ""Name?"";
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
    public void Prompt_ResumeCancelled_ReturnsError()
    {
        var handler = new MockPromptHandler();
        var runtime = CreateRuntimeWithPrompt(handler);

        var story = runtime.LoadStory(@"
            @start
            /prompt ""Name?"";
        ");
        var ctx = runtime.CreateContext(story);
        runtime.Run(ctx);

        var internalCtx = (Zoh.Runtime.Execution.Context)ctx;
        internalCtx.Resume(new WaitCancelled("cancel_code", "Cancelled"), internalCtx.ResumeToken);
        runtime.Run(ctx);

        Assert.Equal(ContextState.Terminated, ctx.State);
        Assert.Contains(internalCtx.LastDiagnostics, d => d.Severity == DiagnosticSeverity.Error && d.Code == "cancel_code");
    }
}
