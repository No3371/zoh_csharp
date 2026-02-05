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
        var source = "/set *x, 10;";
        var story = runtime.LoadStory(source, "test");

        Assert.NotNull(story);
        Assert.Single(story.Statements);
    }

    [Fact]
    public void Run_ExecutesVerbs()
    {
        var runtime = new ZohRuntime();
        var source = @"
/set *x, 10;
/increase *x, 5;
";
        var story = runtime.LoadStory(source, "test");
        var ctx = runtime.CreateContext(story);

        runtime.Run(ctx, story);

        Assert.Equal(new ZohInt(15), ctx.Variables.Get("x"));
        Assert.Equal(ContextState.Terminated, ctx.State);
    }

    [Fact]
    public void Run_Defer_ExecutesOnTermination()
    {
        var runtime = new ZohRuntime();
        var source = @"
/set *x, 0;
/defer /increase *x, 1;;
";
        var story = runtime.LoadStory(source, "test");
        var ctx = runtime.CreateContext(story);

        runtime.Run(ctx, story);

        // Defer runs at termination.
        // x should be 1.
        Assert.Equal(new ZohInt(1), ctx.Variables.Get("x"));
    }
}
