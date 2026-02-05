using Xunit;
using Zoh.Runtime.Verbs;
using Zoh.Runtime.Verbs.Core;

namespace Zoh.Tests.Verbs;

public class VerbSpecComplianceTests
{
    [Fact]
    public void Core_Verbs_Are_Registered()
    {
        var registry = new VerbRegistry();
        registry.RegisterCoreVerbs();

        // Verify Set, Get, Drop, Capture, Type, Increase, Decrease
        Assert.NotNull(registry.GetDriver("core", "set"));
        Assert.NotNull(registry.GetDriver("core", "get"));
        Assert.NotNull(registry.GetDriver("core", "drop"));
        Assert.NotNull(registry.GetDriver("core", "capture"));
        Assert.NotNull(registry.GetDriver("core", "type"));
        Assert.NotNull(registry.GetDriver("core", "increase"));
        Assert.NotNull(registry.GetDriver("core", "decrease"));

        // Verify Interpolate
        Assert.NotNull(registry.GetDriver("core", "interpolate"));

        // Verify Debug Verbs
        Assert.NotNull(registry.GetDriver("core", "info"));
        Assert.NotNull(registry.GetDriver("core", "warning"));
        Assert.NotNull(registry.GetDriver("core", "error"));
        Assert.NotNull(registry.GetDriver("core", "fatal"));

        // Assert shared instance if desired, or just existence.
        Assert.Same(registry.GetDriver("core", "info"), registry.GetDriver("core", "error"));
    }

    [Fact]
    public void Missing_Core_Verbs_Check()
    {
        // This test serves as a TODO list. Uncomment as implemented.
        var registry = new VerbRegistry();
        registry.RegisterCoreVerbs();

        Assert.NotNull(registry.GetDriver("core", "parse"));
        // Assert.NotNull(registry.GetDriver("core", "defer"));

        Assert.NotNull(registry.GetDriver("core", "has"));
        Assert.NotNull(registry.GetDriver("core", "any"));
        Assert.NotNull(registry.GetDriver("core", "first"));
        Assert.NotNull(registry.GetDriver("core", "append"));

        Assert.NotNull(registry.GetDriver("core", "roll"));
        Assert.NotNull(registry.GetDriver("core", "wroll"));
        Assert.NotNull(registry.GetDriver("core", "rand"));
    }
}
