namespace Zoh.Tests.Execution;

using Xunit;
using Zoh.Runtime.Execution;
using Zoh.Runtime.Types;
using Zoh.Runtime.Verbs;
using Zoh.Runtime.Verbs.Standard.Presentation;

public class ApiSurfaceTests
{
    // --- ContextHandle ---

    [Fact]
    public void StartContext_ReturnsHandle_WithIdAndRunningState()
    {
        var runtime = new ZohRuntime();
        var story = runtime.LoadStory("Test\n===\n/set *x, 1;");
        var handle = runtime.StartContext(story);

        Assert.NotNull(handle);
        Assert.NotEmpty(handle.Id);
        Assert.Equal(ContextState.Running, handle.State);
    }

    [Fact]
    public void ContextHandle_State_TracksContextLifecycle()
    {
        var runtime = new ZohRuntime();
        var story = runtime.LoadStory("Test\n===\n/set *x, 1;");
        var handle = runtime.StartContext(story);

        Assert.Equal(ContextState.Running, handle.State);
        runtime.Tick(0);
        Assert.Equal(ContextState.Terminated, handle.State);
    }

    // --- Tick ---

    [Fact]
    public void Tick_DrivesContextToCompletion()
    {
        var runtime = new ZohRuntime();
        var story = runtime.LoadStory("Test\n===\n/set *x, 42;");
        var handle = runtime.StartContext(story);

        runtime.Tick(0);

        Assert.Equal(ContextState.Terminated, handle.State);
    }

    [Fact]
    public void Tick_ResolvesSleepingContext()
    {
        var runtime = new ZohRuntime();
        var story = runtime.LoadStory("Test\n===\n/sleep 0.1;\n/set *x, 1;");  // 0.1s = 100ms
        var handle = runtime.StartContext(story);

        runtime.Tick(0);  // Runs until sleep, blocks
        Assert.Equal(ContextState.Sleeping, handle.State);

        runtime.Tick(50);  // Not enough time
        Assert.Equal(ContextState.Sleeping, handle.State);

        runtime.Tick(60);  // Enough time (total 110ms >= 100ms wake)
        Assert.Equal(ContextState.Terminated, handle.State);
    }

    [Fact]
    public void Tick_MultipleContexts_AllDriven()
    {
        var runtime = new ZohRuntime();
        var story1 = runtime.LoadStory("S1\n===\n/set *a, 1;");
        var story2 = runtime.LoadStory("S2\n===\n/set *b, 2;");
        var h1 = runtime.StartContext(story1);
        var h2 = runtime.StartContext(story2);

        runtime.Tick(0);

        Assert.Equal(ContextState.Terminated, h1.State);
        Assert.Equal(ContextState.Terminated, h2.State);
    }

    // --- Resume ---

    [Fact]
    public void Resume_UnblocksWaitingHostContext()
    {
        var runtime = new ZohRuntime();
        ContextHandle? capturedHandle = null;

        runtime.VerbRegistry.Register(new ConverseDriver(
            new TestConverseHandler(h => capturedHandle = h)));

        var story = runtime.LoadStory("Test\n===\n/converse \"Hello\";");
        var handle = runtime.StartContext(story);

        runtime.Tick(0);  // Runs until converse blocks
        Assert.Equal(ContextState.WaitingHost, handle.State);
        Assert.NotNull(capturedHandle);
        Assert.Equal(handle.Id, capturedHandle!.Id);

        runtime.Resume(handle, ZohValue.Nothing);
        runtime.Tick(0);  // Continue after resume
        Assert.Equal(ContextState.Terminated, handle.State);
    }

    // --- ExecutionResult ---

    [Fact]
    public void GetResult_ReturnsValueAndVariables()
    {
        var runtime = new ZohRuntime();
        var story = runtime.LoadStory("Test\n===\n/set *x, 42;");
        var handle = runtime.StartContext(story);
        runtime.Tick(0);

        var result = runtime.GetResult(handle);

        Assert.NotNull(result);
        Assert.True(result.Variables.Has("x"));
        Assert.Equal(new ZohInt(42), result.Variables.Get("x"));
    }

    [Fact]
    public void GetResult_ThrowsForNonTerminated()
    {
        var runtime = new ZohRuntime();
        var story = runtime.LoadStory("Test\n===\n/sleep 1000;");
        var handle = runtime.StartContext(story);
        runtime.Tick(0);

        Assert.Throws<InvalidOperationException>(() => runtime.GetResult(handle));
    }

    [Fact]
    public void VariableAccessor_Keys_ReturnsAllNames()
    {
        var runtime = new ZohRuntime();
        var story = runtime.LoadStory("Test\n===\n/set *a, 1;\n/set *b, 2;");
        var handle = runtime.StartContext(story);
        runtime.Tick(0);

        var result = runtime.GetResult(handle);
        var keys = result.Variables.Keys();

        Assert.Contains("a", keys);
        Assert.Contains("b", keys);
    }

    // --- Test helper ---

    private class TestConverseHandler : IConverseHandler
    {
        private readonly Action<ContextHandle> _onConverse;

        public TestConverseHandler(Action<ContextHandle> onConverse)
            => _onConverse = onConverse;

        public void OnConverse(ContextHandle handle, ConverseRequest request)
            => _onConverse(handle);
    }
}
