using Xunit;
using Zoh.Runtime.Execution;
using Zoh.Runtime.Types;

namespace Zoh.Tests.Execution;

public class ChannelManagerTests
{
    [Fact]
    public void Open_CreatesChannel()
    {
        var manager = new ChannelManager();
        var gen = manager.Open("test");

        Assert.True(gen > 0);
        Assert.True(manager.Exists("test"));
    }

    [Fact]
    public void Open_ExistingOpen_NoOp()
    {
        var manager = new ChannelManager();
        var gen1 = manager.Open("test");
        var gen2 = manager.Open("test");

        Assert.Equal(gen1, gen2);
    }

    [Fact]
    public void Open_ClosedChannel_IncrementsGeneration()
    {
        var manager = new ChannelManager();
        var gen1 = manager.Open("test");
        manager.TryClose("test");
        var gen2 = manager.Open("test");

        Assert.True(gen2 > gen1, $"Expected gen2 ({gen2}) > gen1 ({gen1})");
    }

    [Fact]
    public void TryPush_NonExistent_ReturnsFalse()
    {
        var manager = new ChannelManager();
        var result = manager.TryPush("test", new ZohInt(42));

        Assert.False(result);
    }

    [Fact]
    public void TryPush_ClosedChannel_ReturnsFalse()
    {
        var manager = new ChannelManager();
        manager.Open("test");
        manager.TryClose("test");
        var result = manager.TryPush("test", new ZohInt(42));

        Assert.False(result);
    }

    [Fact]
    public void TryPush_OpenChannel_ReturnsTrue()
    {
        var manager = new ChannelManager();
        manager.Open("test");
        var result = manager.TryPush("test", new ZohInt(42));

        Assert.True(result);
        Assert.Equal(1, manager.Count("test"));
    }

    [Fact]
    public void TryPull_WrongGeneration_ReturnsGenerationMismatch()
    {
        var manager = new ChannelManager();
        var gen1 = manager.Open("test");
        manager.TryPush("test", new ZohInt(42));
        manager.TryClose("test");
        manager.Open("test");  // New channel with new generation

        var result = manager.TryPull("test", gen1);  // Old generation

        Assert.Equal(PullStatus.GenerationMismatch, result.Status);
    }

    [Fact]
    public void TryPull_CorrectGeneration_ReturnsValue()
    {
        var manager = new ChannelManager();
        var gen = manager.Open("test");
        manager.TryPush("test", new ZohInt(42));

        var result = manager.TryPull("test", gen);

        Assert.Equal(PullStatus.Success, result.Status);
        Assert.IsType<ZohInt>(result.Value);
    }

    [Fact]
    public void ChannelName_CaseInsensitive()
    {
        var manager = new ChannelManager();
        manager.Open("TEST");

        Assert.True(manager.Exists("test"));
        Assert.True(manager.Exists("Test"));
        Assert.True(manager.Exists("TEST"));
    }
}
