using Zoh.Runtime.Diagnostics;
using Zoh.Runtime.Execution;
using Zoh.Runtime.Preprocessing;
using Zoh.Runtime.Validation;

namespace Zoh.Tests.Execution;

public class CompilationPipelineTests
{
    [Fact]
    public void LoadStory_RunsThroughPipeline_Success()
    {
        var runtime = new ZohRuntime();
        var source = "Test\n===\n/set *a, 1;";

        var story = runtime.LoadStory(source);

        Assert.NotNull(story);
        Assert.Equal(1, story.Statements.Length);
    }

    [Fact]
    public void LoadStory_InvocationPreprocessor()
    {
        var runtime = new ZohRuntime();
        bool invoked = false;

        var pp = new ActionPreprocessor(ctx =>
        {
            invoked = true;
            return new PreprocessorResult(ctx.SourceText, null, []);
        });

        runtime.Handlers.RegisterPreprocessor(pp);
        runtime.LoadStory("Test\n===\n/set *a, 1;");

        Assert.True(invoked);
    }

    [Fact]
    public void LoadStory_InvocationValidator()
    {
        var runtime = new ZohRuntime();
        bool invoked = false;

        var val = new ActionValidator(story =>
        {
            invoked = true;
            return [];
        });

        runtime.Handlers.RegisterStoryValidator(val);
        runtime.LoadStory("Test\n===\n/set *a, 1;");

        Assert.True(invoked);
    }

    [Fact]
    public void LoadStory_FatalValidator_ThrowsCompilationException()
    {
        var runtime = new ZohRuntime();

        var val = new ActionValidator(story =>
        {
            return [new Diagnostic(DiagnosticSeverity.Fatal, "TEST", "Fatal error", default)];
        });

        runtime.Handlers.RegisterStoryValidator(val);

        var ex = Assert.Throws<CompilationException>(() => runtime.LoadStory("Test\n===\n/set *a, 1;"));
        Assert.Contains(ex.Diagnostics, d => d.Code == "TEST");
    }

    // Helpers

    private class ActionPreprocessor(Func<PreprocessorContext, PreprocessorResult> action) : IPreprocessor
    {
        public int Priority => 0;
        public PreprocessorResult Process(PreprocessorContext context) => action(context);
    }

    private class ActionValidator(Func<CompiledStory, IReadOnlyList<Diagnostic>> action) : IStoryValidator
    {
        public int Priority => 0;
        public IReadOnlyList<Diagnostic> Validate(CompiledStory story) => action(story);
    }
}
