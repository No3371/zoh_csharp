using System.Collections.Generic;
using Xunit;
using Zoh.Runtime.Execution;
using Zoh.Runtime.Types;
using Zoh.Runtime.Variables;
using Zoh.Runtime.Parsing.Ast;
using System.Collections.Immutable;
using Zoh.Runtime.Storage;

namespace Zoh.Tests.Execution;

public class ContextTests
{
    private readonly VariableStore _store;
    private readonly Context _context;

    public ContextTests()
    {
        _store = new VariableStore(new Dictionary<string, Variable>());
        _context = new Context(_store, new InMemoryStorage(), new ChannelManager(), new SignalManager());
    }

    [Fact]
    public void Context_State_IsRunningByDefault()
    {
        Assert.Equal(ContextState.Running, _context.State);
    }

    [Fact]
    public void SetState_UpdatesState()
    {
        _context.SetState(ContextState.Sleeping);
        Assert.Equal(ContextState.Sleeping, _context.State);
    }

    [Fact]
    public void Terminate_ExecutesDeffers()
    {
        // Mock VerbExecutor (since context doesn't have internal runner)
        var executed = new List<string>();
        _context.VerbExecutor = (verb, ctx) =>
        {
            if (verb is ValueAst.String s) executed.Add(s.Value);
            return Zoh.Runtime.Verbs.DriverResult.Complete.Ok();
        };

        // Add defers (LIFO)
        // Story Defers (Inner scope, popped first in terminate logic usually? Wait, Context calls ExecuteDefers(story), ExecuteDefers(context)?)
        // impl/06_core_verbs.md: "On story exit: Context.executeStoryDefers()... On context termination: Context.executeContextDefers()"
        // But Context.Terminate() does: ExecuteDefers(_storyDefers); ExecuteDefers(_contextDefers);
        // So order is Story stack then Context stack.

        _context.AddStoryDefer(new ValueAst.String("s1"));
        _context.AddStoryDefer(new ValueAst.String("s2"));

        _context.AddContextDefer(new ValueAst.String("c1"));
        _context.AddContextDefer(new ValueAst.String("c2"));

        _context.Terminate();

        Assert.Equal(ContextState.Terminated, _context.State);

        // Expected order:
        // Story: s2 -> s1 (LIFO)
        // Context: c2 -> c1 (LIFO)
        Assert.Equal(new[] { "s2", "s1", "c2", "c1" }, executed.ToArray());
    }
}
