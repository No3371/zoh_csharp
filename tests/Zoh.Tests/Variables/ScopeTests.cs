using Xunit;
using Zoh.Runtime.Variables;
using Zoh.Runtime.Types;

namespace Zoh.Tests.Variables;

public class ScopeTests
{

    [Fact]
    public void Story_Shadows_Context_Check()
    {
        // Setup:
        // Story has "v" = 10 (via Set default)
        // Context has "v" = 20 (via injected dictionary)
        // Expect Get("v") == 10 because Story shadows Context (per VariableStore logic).

        var contextDict = new Dictionary<string, Variable>
        {
            { "v", new Variable(new ZohInt(20)) }
        };
        var store = new VariableStore(contextDict);

        // Use Set to put value in Story scope (as we can't inject it)
        store.Set("v", new ZohInt(10), Scope.Story);

        Assert.Equal(new ZohInt(10), store.Get("v"));
    }

    [Fact]
    public void Fallthrough_To_Context()
    {
        // Story missing "v". Context has "v".
        var contextDict = new Dictionary<string, Variable>
        {
            { "v", new Variable(new ZohInt(20)) }
        };
        var store = new VariableStore(contextDict);

        Assert.Equal(new ZohInt(20), store.Get("v"));
    }

    [Fact]
    public void Set_DefaultsTo_Story()
    {
        // Set("v", 1) without scope attribute should go to Story.
        var contextDict = new Dictionary<string, Variable>();
        var store = new VariableStore(contextDict);

        store.Set("v", new ZohInt(1));

        Assert.False(contextDict.ContainsKey("v"), "Context should NOT contain 'v'");
        Assert.Equal(new ZohInt(1), store.Get("v"));
    }

    [Fact]
    public void Set_Explicit_Context()
    {
        var contextDict = new Dictionary<string, Variable>();
        var store = new VariableStore(contextDict);

        store.Set("v", new ZohInt(1), Scope.Context);

        Assert.True(contextDict.ContainsKey("v"));
        Assert.Equal(new ZohInt(1), contextDict["v"].Value);
    }

    [Fact]
    public void Set_Explicit_Story()
    {
        var contextDict = new Dictionary<string, Variable>();
        var store = new VariableStore(contextDict);

        store.Set("v", new ZohInt(1), Scope.Story);

        Assert.False(contextDict.ContainsKey("v"));
        Assert.Equal(new ZohInt(1), store.Get("v"));
    }

    [Fact]
    public void Drop_Story_Reveals_Context()
    {
        // Setup: Context has "x" = 100, Story has "x" = 50
        var contextDict = new Dictionary<string, Variable>
        {
            { "x", new Variable(new ZohInt(100)) }
        };
        var store = new VariableStore(contextDict);
        store.Set("x", new ZohInt(50), Scope.Story);

        // Before Drop: Story shadows Context
        Assert.Equal(new ZohInt(50), store.Get("x"));

        // After Drop: Context revealed
        store.Drop("x");
        Assert.Equal(new ZohInt(100), store.Get("x"));
    }
}
