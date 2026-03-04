using Xunit;
using Zoh.Runtime.Execution;
using Zoh.Runtime.Verbs.Standard.Presentation;
using Zoh.Runtime.Parsing.Ast;
using Zoh.Runtime.Lexing;
using Zoh.Runtime.Types;
using System.Collections.Generic;
using System.Linq;

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
}
