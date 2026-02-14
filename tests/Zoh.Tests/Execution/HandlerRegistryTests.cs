using Zoh.Runtime.Diagnostics;
using Zoh.Runtime.Execution;
using Zoh.Runtime.Parsing.Ast;
using Zoh.Runtime.Preprocessing;
using Zoh.Runtime.Validation;
using Zoh.Runtime.Verbs;

namespace Zoh.Tests.Execution;

public class HandlerRegistryTests
{
    private class MockPreprocessor : IPreprocessor
    {
        public int Priority { get; init; }
        public PreprocessorResult Process(PreprocessorContext context) => new(context.SourceText, null, []);
    }

    private class MockStoryValidator : IStoryValidator
    {
        public int Priority { get; init; }
        public IReadOnlyList<Diagnostic> Validate(CompiledStory story) => Array.Empty<Diagnostic>();
    }

    private class MockVerbValidator : IVerbValidator
    {
        public string VerbName { get; init; } = "test";
        public int Priority { get; init; }
        public IReadOnlyList<Diagnostic> Validate(VerbCallAst call, CompiledStory story) => Array.Empty<Diagnostic>();
    }

    [Fact]
    public void RegisterPreprocessor_SortsByPriority()
    {
        var registry = new HandlerRegistry();
        var p1 = new MockPreprocessor { Priority = 100 };
        var p2 = new MockPreprocessor { Priority = 10 };
        var p3 = new MockPreprocessor { Priority = 50 };

        registry.RegisterPreprocessor(p1);
        registry.RegisterPreprocessor(p2);
        registry.RegisterPreprocessor(p3);

        Assert.Equal(p2, registry.Preprocessors[0]);
        Assert.Equal(p3, registry.Preprocessors[1]);
        Assert.Equal(p1, registry.Preprocessors[2]);
    }

    [Fact]
    public void RegisterStoryValidator_SortsByPriority()
    {
        var registry = new HandlerRegistry();
        var v1 = new MockStoryValidator { Priority = 100 };
        var v2 = new MockStoryValidator { Priority = 10 };

        registry.RegisterStoryValidator(v1);
        registry.RegisterStoryValidator(v2);

        Assert.Equal(v2, registry.StoryValidators[0]);
        Assert.Equal(v1, registry.StoryValidators[1]);
    }

    [Fact]
    public void RegisterVerbValidator_GroupsByNameAndSorts()
    {
        var registry = new HandlerRegistry();
        var v1 = new MockVerbValidator { VerbName = "log", Priority = 100 };
        var v2 = new MockVerbValidator { VerbName = "log", Priority = 10 };
        var v3 = new MockVerbValidator { VerbName = "set", Priority = 50 };

        registry.RegisterVerbValidator(v1);
        registry.RegisterVerbValidator(v2);
        registry.RegisterVerbValidator(v3);

        Assert.Equal(2, registry.VerbValidators["log"].Count);
        Assert.Equal(v2, registry.VerbValidators["log"][0]);
        Assert.Equal(v1, registry.VerbValidators["log"][1]);

        Assert.Single(registry.VerbValidators["set"]);
    }

    [Fact]
    public void RegisterCoreHandlers_PopulatesVerbRegistry()
    {
        var registry = new HandlerRegistry();
        registry.RegisterCoreHandlers();

        // Check for a known core verb
        Assert.NotNull(registry.VerbDrivers.GetDriver("", "set"));
    }
}
