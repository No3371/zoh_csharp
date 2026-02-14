using Zoh.Runtime.Execution;

namespace Zoh.Tests.Execution;

public class RuntimeConfigTests
{
    [Fact]
    public void Default_HasExpectedValues()
    {
        var config = RuntimeConfig.Default;

        Assert.Equal(0, config.MaxContexts);
        Assert.Equal(0, config.MaxChannelDepth);
        Assert.Equal(0, config.ExecutionTimeoutMs);
        Assert.True(config.EnableDiagnostics);
    }

    [Fact]
    public void Constructor_CanSetValues()
    {
        var config = new RuntimeConfig
        {
            MaxContexts = 10,
            MaxChannelDepth = 100,
            ExecutionTimeoutMs = 5000,
            EnableDiagnostics = false
        };

        Assert.Equal(10, config.MaxContexts);
        Assert.Equal(100, config.MaxChannelDepth);
        Assert.Equal(5000, config.ExecutionTimeoutMs);
        Assert.False(config.EnableDiagnostics);
    }
}
