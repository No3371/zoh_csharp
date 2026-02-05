using Zoh.Runtime.Verbs;
using System.Collections.Generic;
using Zoh.Runtime.Execution;
using Zoh.Runtime.Parsing.Ast;
using Zoh.Runtime.Types;
using Zoh.Runtime.Lexing;
using Xunit;
using Zoh.Runtime.Validation;
using Zoh.Runtime.Diagnostics;
using System.Collections.Immutable;

namespace Zoh.Tests.Verbs;

public class NamespaceTests
{
    private readonly VerbRegistry _registry = new();

    private class TestVerbDriver(string ns, string name) : IVerbDriver
    {
        public string Namespace => ns;
        public string Name => name;
        public int Priority => 0;

        public VerbResult Execute(IExecutionContext context, VerbCallAst verbCall)
        {
            return VerbResult.Ok();
        }

        public IReadOnlyList<Diagnostic> Validate(VerbCallAst verbCall, IExecutionContext context) => [];
    }

    [Fact]
    public void Register_IndicesSuffixes()
    {
        _registry.Register(new TestVerbDriver("std.core", "log"));

        // Should resolve for:
        // "log"
        var res1 = _registry.Resolve("log");
        Assert.Equal(ResolutionStatus.Success, res1.Status);
        Assert.Equal("log", res1.Driver!.Name);

        // "core.log"
        var res2 = _registry.Resolve("core.log");
        Assert.Equal(ResolutionStatus.Success, res2.Status);

        // "std.core.log"
        var res3 = _registry.Resolve("std.core.log");
        Assert.Equal(ResolutionStatus.Success, res3.Status);
    }

    [Fact]
    public void Resolve_Ambiguous_ReturnsCandidates()
    {
        _registry.Register(new TestVerbDriver("std", "log"));
        _registry.Register(new TestVerbDriver("my", "log"));

        // "log" is ambiguous
        var res = _registry.Resolve("log");
        Assert.Equal(ResolutionStatus.Ambiguous, res.Status);
        Assert.Equal(2, res.Candidates.Count);

        // "std.log" is unique
        var resStd = _registry.Resolve("std.log");
        Assert.Equal(ResolutionStatus.Success, resStd.Status);
        Assert.Equal("std", resStd.Driver!.Namespace);

        // "my.log" is unique
        var resMy = _registry.Resolve("my.log");
        Assert.Equal(ResolutionStatus.Success, resMy.Status);
        Assert.Equal("my", resMy.Driver!.Namespace);
    }

    [Fact]
    public void Resolve_ExactMatch_IsPreferred_Or_HandledViaUniqueSuffix()
    {
        // Case: "log" and "std.log". User types "log".
        // Driver A: ns="std", name="log". Suffixes: "log", "std.log".
        // Driver B: ns="", name="log". Suffixes: "log".

        // If we register "std.log" and "log" (global).
        // "log" matches both Suffix "log".
        // This is AMBIGUOUS by default suffix logic.
        // User must type "std.log" to get std version.
        // But how to get global version? "log"? Still ambiguous.
        // Unless global verbs have empty namespace?

        var globalLog = new TestVerbDriver("", "log");
        var stdLog = new TestVerbDriver("std", "log");

        _registry.Register(globalLog);
        _registry.Register(stdLog);

        var res = _registry.Resolve("log");
        // Expect Ambiguous? Or does global win?
        // Suffix "log" -> [globalLog, stdLog].
        // It is AMBIGUOUS.
        // This means if you have `std.set`, you CANNOT call `/set` without ambiguity if `core.set` exists.
        // `core.set` is global default.
        // If I make `my.set`, `/set` breaks with ambiguity.
        // This forces user to use `/core.set` or `/my.set`.
        // This is correct spec behavior: "Ambiguous calls result in fatal error".

        Assert.Equal(ResolutionStatus.Ambiguous, res.Status);
    }

    [Fact]
    public void CaseInsensitivity_Works()
    {
        _registry.Register(new TestVerbDriver("std", "LoG"));

        var res = _registry.Resolve("log"); // Lowercase query
        Assert.Equal(ResolutionStatus.Success, res.Status);
        Assert.Equal("LoG", res.Driver!.Name);

        var res2 = _registry.Resolve("STD.LOG"); // Uppercase query
        Assert.Equal(ResolutionStatus.Success, res2.Status);
    }

    [Fact]
    public void Validation_Catches_Ambiguity()
    {
        _registry.Register(new TestVerbDriver("a", "verb"));
        _registry.Register(new TestVerbDriver("b", "verb"));

        // AST with /verb;
        var call = new VerbCallAst(null, "verb", false, [], ImmutableDictionary<string, ValueAst>.Empty, [], new TextPosition(1, 1, 0));
        var stmt = new StatementAst.VerbCall(call);
        var story = new StoryAst("test", ImmutableDictionary<string, ValueAst>.Empty, ImmutableArray.Create<StatementAst>(stmt), ImmutableDictionary<string, int>.Empty);

        var validator = new NamespaceValidator(_registry);
        var result = validator.Validate(story);

        Assert.False(result.IsSuccess);
        Assert.Contains("Ambiguous verb", result.Errors[0].Message);
    }

    [Fact]
    public void Validation_Catches_Unknown()
    {
        var call = new VerbCallAst(null, "unknown", false, [], ImmutableDictionary<string, ValueAst>.Empty, [], new TextPosition(1, 1, 0));
        var stmt = new StatementAst.VerbCall(call);
        var story = new StoryAst("test", ImmutableDictionary<string, ValueAst>.Empty, ImmutableArray.Create<StatementAst>(stmt), ImmutableDictionary<string, int>.Empty);

        var validator = new NamespaceValidator(_registry);
        var result = validator.Validate(story);

        Assert.False(result.IsSuccess);
        Assert.Contains("Unknown verb", result.Errors[0].Message);
    }
}
