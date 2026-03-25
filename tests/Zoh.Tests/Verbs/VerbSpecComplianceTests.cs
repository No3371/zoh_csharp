using Xunit;
using Zoh.Runtime.Verbs;

namespace Zoh.Tests.Verbs;

public class VerbSpecComplianceTests
{
    [Fact]
    public void Core_Verbs_Are_Registered()
    {
        var registry = new VerbRegistry();
        registry.RegisterCoreVerbs();

        // Suffix resolution: short names still resolve after core.{group}.{name} alignment.
        Assert.NotNull(registry.GetDriver(null, "set"));
        Assert.NotNull(registry.GetDriver(null, "get"));
        Assert.NotNull(registry.GetDriver(null, "drop"));
        Assert.NotNull(registry.GetDriver(null, "capture"));
        Assert.NotNull(registry.GetDriver(null, "type"));
        Assert.NotNull(registry.GetDriver(null, "increase"));
        Assert.NotNull(registry.GetDriver(null, "decrease"));

        Assert.NotNull(registry.GetDriver(null, "interpolate"));

        Assert.NotNull(registry.GetDriver(null, "info"));
        Assert.NotNull(registry.GetDriver(null, "warning"));
        Assert.NotNull(registry.GetDriver(null, "error"));
        Assert.NotNull(registry.GetDriver(null, "fatal"));

        Assert.Same(registry.GetDriver(null, "info"), registry.GetDriver(null, "error"));
    }

    [Fact]
    public void Missing_Core_Verbs_Check()
    {
        var registry = new VerbRegistry();
        registry.RegisterCoreVerbs();

        Assert.NotNull(registry.GetDriver(null, "parse"));

        Assert.NotNull(registry.GetDriver(null, "has"));
        Assert.NotNull(registry.GetDriver(null, "any"));
        Assert.NotNull(registry.GetDriver(null, "first"));
        Assert.NotNull(registry.GetDriver(null, "append"));

        Assert.NotNull(registry.GetDriver(null, "roll"));
        Assert.NotNull(registry.GetDriver(null, "wroll"));
        Assert.NotNull(registry.GetDriver(null, "rand"));
    }
}
