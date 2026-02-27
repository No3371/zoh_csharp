using Xunit;
using Zoh.Runtime.Execution;
using Zoh.Runtime.Types;
using Zoh.Runtime.Variables;

namespace Zoh.Tests.Execution;

public class RuntimeTests
{
    [Fact]
    public void LoadStory_ParsesSource()
    {
        var runtime = new ZohRuntime();
        var source = @"test story
===
/set *x, 10;";
        var story = runtime.LoadStory(source);

        Assert.NotNull(story);
        Assert.Single(story.Statements);
    }

    [Fact]
    public void Run_ExecutesVerbs()
    {
        var runtime = new ZohRuntime();
        var source = @"runtime requires story name
===
/set *x, 10;
/increase *x, 5;
";
        var story = runtime.LoadStory(source);
        var ctx = runtime.CreateContext(story);

        runtime.Run(ctx);

        Assert.Equal(new ZohInt(15), ctx.Variables.Get("x"));
        Assert.Equal(ContextState.Terminated, ctx.State);
    }

    [Fact]
    public void Run_Defer_ExecutesOnTermination()
    {
        var runtime = new ZohRuntime();
        var source = @"test
===
/set *x, 0;
/defer /increase *x, 1;;
";
        var story = runtime.LoadStory(source);
        var ctx = runtime.CreateContext(story);

        runtime.Run(ctx);

        // Defer runs at termination.
        // x should be 1.
        Assert.Equal(new ZohInt(1), ctx.Variables.Get("x"));
    }

    [Fact]
    public void RunToCompletion_ReturnsLastResult()
    {
        var runtime = new ZohRuntime();
        var source = @"test
===
/set *x, 42;
/get *x;
";
        var story = runtime.LoadStory(source);
        var ctx = runtime.CreateContext(story);

        var result = runtime.RunToCompletion(ctx);

        Assert.Equal(ContextState.Terminated, ctx.State);
        // /get returns the variable's value
        Assert.Equal(new ZohInt(42), result);
    }

    [Fact]
    public void RunToCompletion_EmptyStory_ReturnsNothing()
    {
        var runtime = new ZohRuntime();
        var source = @"empty
===
";
        var story = runtime.LoadStory(source);
        var ctx = runtime.CreateContext(story);

        var result = runtime.RunToCompletion(ctx);

        Assert.Equal(ContextState.Terminated, ctx.State);
        Assert.Equal(ZohNothing.Instance, result);
    }
}
