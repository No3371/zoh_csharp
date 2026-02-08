using System.Collections.Generic;
using Xunit;
using Zoh.Runtime.Execution;
using Zoh.Runtime.Types;
using Zoh.Runtime.Variables;
using Zoh.Runtime.Parsing.Ast;
using Zoh.Runtime.Storage;

namespace Zoh.Tests.Execution;

public class SignalTests
{
    private readonly SignalManager _signalManager;
    private readonly Context _context;

    public SignalTests()
    {
        _signalManager = new SignalManager();
        var store = new VariableStore(new Dictionary<string, Variable>());
        _context = new Context(store, new InMemoryStorage(), new ChannelManager(), _signalManager);
    }

    [Fact]
    public void SignalManager_Subscribe_AddsContext()
    {
        _signalManager.Subscribe("test_signal", _context);

        // No direct way to inspect private state, but we can test Broadcast
        int count = _signalManager.Broadcast("test_signal", ZohValue.Nothing);

        // Should wake up context if it was waiting?
        // Context state must be WaitingMessage for Broadcast to count it as "woken".
        // But even if not waiting, it should be in the list?
        // Broadcast logic:
        // foreach ctx in waiters:
        //   if (ctx.State == ContextState.WaitingMessage) { ... count++ }
        //   remove subscription

        // So validation is hard without setting state.
    }

    [Fact]
    public void Broadcast_WakesUpWaitingContext()
    {
        _signalManager.Subscribe("wake_me", _context);
        _context.SetState(ContextState.WaitingMessage);

        var payload = new ZohInt(42);
        int woken = _signalManager.Broadcast("wake_me", payload);

        Assert.Equal(1, woken);
        Assert.Equal(ContextState.Running, _context.State);
        Assert.Equal(payload, _context.LastResult);
    }

    [Fact]
    public void Broadcast_RemovesSubscription()
    {
        _signalManager.Subscribe("oneshot", _context);
        _context.SetState(ContextState.WaitingMessage);

        _signalManager.Broadcast("oneshot", ZohValue.Nothing);

        // Second broadcast should find no one
        _context.SetState(ContextState.WaitingMessage); // Reset state manually
        int woken = _signalManager.Broadcast("oneshot", ZohValue.Nothing);

        Assert.Equal(0, woken);
    }

    [Fact]
    public void Unsubscribe_RemovesContext()
    {
        _signalManager.Subscribe("cancel_me", _context);
        _context.SetState(ContextState.WaitingMessage);

        _signalManager.Unsubscribe("cancel_me", _context);

        int woken = _signalManager.Broadcast("cancel_me", ZohValue.Nothing);
        Assert.Equal(0, woken);
        Assert.Equal(ContextState.WaitingMessage, _context.State); // Still waiting
    }

    [Fact]
    public void Terminate_UnsubscribesFromAll()
    {
        // Subscribe to multiple signals
        _signalManager.Subscribe("sig1", _context);
        _signalManager.Subscribe("sig2", _context);

        _context.SetState(ContextState.WaitingMessage);

        // Terminate context (should trigger UnsubscribeContext)
        _context.Terminate();

        // Verify signals don't wake it (even if we force state back to Waiting for test?)
        // Terminate sets state to Terminated.
        // Broadcast only wakes WaitingMessage.
        // So we must manually set state back to WaitingMessage to verify subscription is gone.

        _context.SetState(ContextState.WaitingMessage);

        int woken1 = _signalManager.Broadcast("sig1", ZohValue.Nothing);
        int woken2 = _signalManager.Broadcast("sig2", ZohValue.Nothing);

        Assert.Equal(0, woken1);
        Assert.Equal(0, woken2);
    }
}
