using Xunit;
using Zoh.Runtime.Execution;
using Zoh.Runtime.Verbs.Standard.Presentation;
using Zoh.Runtime.Parsing.Ast;
using Zoh.Runtime.Lexing;
using Zoh.Runtime.Types;
using System.Collections.Generic;
using System.Linq;
using Zoh.Runtime.Diagnostics;
using Zoh.Runtime.Verbs;

namespace Zoh.Tests.Verbs.Standard.Presentation;

public class ConverseDriverTests
{
    private class MockConverseHandler : IConverseHandler
    {
        public List<ConverseRequest> Requests { get; } = new();

        public void OnConverse(ContextHandle handle, ConverseRequest request)
        {
            Requests.Add(request);
        }
    }

    private ZohRuntime CreateRuntimeWithConverse(IConverseHandler handler)
    {
        var runtime = new ZohRuntime();
        runtime.Handlers.RegisterCoreHandlers(); // Registers standard verbs and validators implicitly since we added them in step 5

        // Override the registered ConverseDriver to use our mock handler instead of the default one
        var driver = new ConverseDriver(handler);
        runtime.VerbRegistry.Register(driver);

        return runtime;
    }

    [Fact]
    public void Converse_BasicWait_YieldsHostContinuation()
    {
        var handler = new MockConverseHandler();
        var runtime = CreateRuntimeWithConverse(handler);

        CompiledStory story;
        try
        {
            story = runtime.LoadStory(@"
            @start
            /converse ""Hello World"";
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
        Assert.Single(handler.Requests);
        Assert.Equal("Hello World", handler.Requests[0].Contents[0]);
        Assert.Null(handler.Requests[0].Tag);
    }

    [Fact]
    public void Converse_WithTagAttribute_PassesTagToHandler()
    {
        var handler = new MockConverseHandler();
        var runtime = CreateRuntimeWithConverse(handler);

        CompiledStory story;
        try
        {
            story = runtime.LoadStory(@"
            @start
            /converse [tag:""line-1""] ""Hello World"";
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
        var request = Assert.Single(handler.Requests);
        Assert.Equal("line-1", request.Tag);
    }

    [Fact]
    public void Converse_InteractiveFalse_DoesNotWait()
    {
        var handler = new MockConverseHandler();
        var runtime = CreateRuntimeWithConverse(handler);

        // Set interactive to false in context or via [wait:false]
        CompiledStory story;
        try
        {
            story = runtime.LoadStory(@"
            @start
            /converse [wait:false] ""Hello World"";
            ");
        }
        catch (CompilationException ex)
        {
            var msg = string.Join("\n", ex.Diagnostics.Select(d => d.Message));
            throw new System.Exception("Compilation failed: " + msg, ex);
        }
        var ctx = runtime.CreateContext(story);

        runtime.Run(ctx);

        Assert.Equal(ContextState.Terminated, ctx.State); // Should run to completion
        Assert.Single(handler.Requests);
        Assert.Equal("Hello World", handler.Requests[0].Contents[0]);
    }

    [Fact]
    public void Converse_AttributesParsed()
    {
        var handler = new MockConverseHandler();
        var runtime = CreateRuntimeWithConverse(handler);

        CompiledStory story;
        try
        {
            story = runtime.LoadStory(@"
            @start
            /converse [By:""Alice""] [Portrait:""Happy""] [Style:""Shout""] timeout:5.0, ""Watch out!"";
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
        Assert.Equal("Alice", request.Speaker);
        Assert.Equal("Happy", request.Portrait);
        Assert.Equal("Shout", request.Style);
        Assert.Equal(5000.0, request.TimeoutMs);
        Assert.Equal("Watch out!", request.Contents[0]);
    }

    [Fact]
    public void Converse_MultipleContents()
    {
        var handler = new MockConverseHandler();
        var runtime = CreateRuntimeWithConverse(handler);

        CompiledStory story;
        try
        {
            story = runtime.LoadStory(@"
            @start
            /converse ""Line 1"", ""Line 2"", ""Line 3"";
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
        Assert.Equal(3, request.Contents.Count);
        Assert.Equal("Line 1", request.Contents[0]);
        Assert.Equal("Line 2", request.Contents[1]);
        Assert.Equal("Line 3", request.Contents[2]);
    }

    [Fact]
    public void Converse_ResumeTimedOut_ReturnsInfoNothing()
    {
        var handler = new MockConverseHandler();
        var runtime = CreateRuntimeWithConverse(handler);

        var story = runtime.LoadStory(@"
            @start
            /converse ""Talk"";
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
    public void Converse_ResumeCancelled_ReturnsError()
    {
        var handler = new MockConverseHandler();
        var runtime = CreateRuntimeWithConverse(handler);

        var story = runtime.LoadStory(@"
            @start
            /converse ""Talk"";
        ");
        var ctx = runtime.CreateContext(story);
        runtime.Run(ctx);

        var internalCtx = (Zoh.Runtime.Execution.Context)ctx;
        internalCtx.Resume(new WaitCancelled("cancel_code", "Cancelled"), internalCtx.ResumeToken);
        runtime.Run(ctx);

        Assert.Equal(ContextState.Terminated, ctx.State);
        Assert.Contains(internalCtx.LastDiagnostics, d => d.Severity == DiagnosticSeverity.Error && d.Code == "cancel_code");
    }

    [Fact]
    public void Converse_TimeoutZero_ReturnsInfoDiagnosticImmediately()
    {
        var handler = new MockConverseHandler();
        var runtime = CreateRuntimeWithConverse(handler);

        var story = runtime.LoadStory(@"
            @start
            /converse timeout:0, ""Fast"";
        ");
        var ctx = runtime.CreateContext(story);
        runtime.Run(ctx);

        var internalCtx = (Zoh.Runtime.Execution.Context)ctx;
        Assert.Equal(ContextState.Terminated, ctx.State);
        Assert.Empty(handler.Requests);
        Assert.Equal(ZohValue.Nothing, internalCtx.LastResult);
        Assert.Contains(internalCtx.LastDiagnostics, d => d.Severity == DiagnosticSeverity.Info && d.Code == "timeout");
    }

    [Fact]
    public void Converse_TimeoutNegative_ReturnsInfoDiagnosticImmediately()
    {
        var handler = new MockConverseHandler();
        var runtime = CreateRuntimeWithConverse(handler);

        var story = runtime.LoadStory(@"
            @start
            /converse timeout:-1, ""Fast"";
        ");
        var ctx = runtime.CreateContext(story);
        runtime.Run(ctx);

        var internalCtx = (Zoh.Runtime.Execution.Context)ctx;
        Assert.Equal(ContextState.Terminated, ctx.State);
        Assert.Empty(handler.Requests);
        Assert.Contains(internalCtx.LastDiagnostics, d => d.Severity == DiagnosticSeverity.Info && d.Code == "timeout");
    }

    [Fact]
    public void Converse_TimeoutQuestion_NoImmediateTimeout()
    {
        var handler = new MockConverseHandler();
        var runtime = CreateRuntimeWithConverse(handler);

        var story = runtime.LoadStory(@"
            @start
            /converse timeout:?, ""Talk"";
        ");
        var ctx = runtime.CreateContext(story);
        runtime.Run(ctx);

        Assert.Equal(ContextState.WaitingHost, ctx.State);
        Assert.Single(handler.Requests);
        Assert.Null(handler.Requests[0].TimeoutMs);
    }

    [Fact]
    public void Converse_TimeoutPositive_PassesTimeoutMsToHostRequest()
    {
        var handler = new MockConverseHandler();
        var runtime = CreateRuntimeWithConverse(handler);

        var story = runtime.LoadStory(@"
            @start
            /converse timeout:5, ""Talk"";
        ");
        var ctx = runtime.CreateContext(story);
        runtime.Run(ctx);

        Assert.Equal(ContextState.WaitingHost, ctx.State);
        Assert.Single(handler.Requests);
        Assert.Equal(5000.0, handler.Requests[0].TimeoutMs);
    }
}
