using System.Collections.Concurrent;
using Zoh.Runtime.Types;
using Zoh.Runtime.Variables;

namespace Zoh.Tests.Variables;

public class VariableStoreTests
{
    private readonly Dictionary<string, Variable> _contextScope = new();
    private readonly VariableStore _store;

    public VariableStoreTests()
    {
        _store = new VariableStore(_contextScope);
    }

    [Fact]
    public void Get_ReturnsNothing_WhenNotFound()
    {
        var result = _store.Get("missing");
        Assert.Equal(ZohValue.Nothing, result);
    }

    [Fact]
    public void Set_CreatesInStory_ByDefault()
    {
        _store.Set("x", new ZohInt(1));

        Assert.Equal(new ZohInt(1), _store.Get("x"));
        Assert.Empty(_contextScope); // Should NOT be in context (global) scope
    }

    [Fact]
    public void Set_ShadowsContext_ByDefault()
    {
        _contextScope["x"] = new Variable(new ZohInt(1));

        _store.Set("x", new ZohInt(2));

        Assert.Equal(new ZohInt(2), _store.Get("x"));
        Assert.Equal(new ZohInt(1), _contextScope["x"].Value); // Context should remain unchanged
    }

    [Fact]
    public void Set_WithContextScope_ForcesContext()
    {
        _store.Set("x", new ZohInt(1), Scope.Context);

        Assert.Single(_contextScope);
        Assert.Equal(new ZohInt(1), _contextScope["x"].Value);
    }

    [Fact]
    public void Story_Shadows_Context()
    {
        _contextScope["x"] = new Variable(new ZohInt(1)); // Context has 1
        _store.Set("x", new ZohInt(2), Scope.Story);      // Story has 2

        Assert.Equal(new ZohInt(2), _store.Get("x"));     // Should identify Story value (shadowing)
        Assert.Equal(new ZohInt(1), _contextScope["x"].Value); // Context value remained unchanged
    }

    [Fact]
    public void TypeConstraint_IsEnforced()
    {
        _contextScope["x"] = new Variable(new ZohInt(1), ZohValueType.Integer);

        Assert.Throws<InvalidOperationException>(() => _store.Set("x", new ZohStr("fail"), Scope.Context));
    }

    [Fact]
    public void Variable_Names_Are_CaseInsensitive()
    {
        _store.Set("MyVar", new ZohInt(10));

        Assert.Equal(new ZohInt(10), _store.Get("myvar"));
        Assert.Equal(new ZohInt(10), _store.Get("MYVAR"));
    }
    [Theory]
    [InlineData("validName")]
    [InlineData("_underscore")]
    [InlineData("camelCase")]
    [InlineData("ALLCAPS")]
    // Spec says "can NOT start with digits", implies digits allowed elsewhere if lexer supports it (IsIdentifierContinue allows digits)
    public void Set_ValidName_Succeeds(string name)
    {
        _store.Set(name, new ZohInt(1));
        Assert.Equal(new ZohInt(1), _store.Get(name));
    }

    [Fact]
    public void Set_NameWithDigits_AllowedIfNotAtStart()
    {
        _store.Set("var123", new ZohInt(1));
        Assert.Equal(new ZohInt(1), _store.Get("var123"));
    }

    [Theory]
    [InlineData("123startsWithDigit")]
    [InlineData("0abc")]
    [InlineData("9test")]
    public void Set_NameStartsWithDigit_ThrowsOrRejects(string name)
    {
        Assert.ThrowsAny<ArgumentException>(() => _store.Set(name, new ZohInt(1)));
    }

    [Theory]
    [InlineData("has space")]
    [InlineData("tab\there")]
    [InlineData("new\nline")]
    public void Set_NameWithWhitespace_ThrowsOrRejects(string name)
    {
        // Lexer splits these, so "has space" becomes ID "has" and ID "space"
        // But via API we pass "has space" string.
        Assert.ThrowsAny<ArgumentException>(() => _store.Set(name, new ZohInt(1)));
    }

    [Theory]
    [InlineData("has;semicolon")]
    [InlineData("has,comma")]
    [InlineData("has:colon")]
    [InlineData("has[bracket")]
    [InlineData("has]bracket")]
    [InlineData("has{brace")]
    [InlineData("has}brace")]
    [InlineData("has<angle")]
    [InlineData("has>angle")]
    [InlineData("has/slash")]
    [InlineData("has@at")]
    [InlineData("has*star")]
    [InlineData("has#hash")]
    public void Set_NameWithReservedChar_ThrowsOrRejects(string name)
    {
        Assert.ThrowsAny<ArgumentException>(() => _store.Set(name, new ZohInt(1)));
    }

    [Fact]
    public void Set_NameWithDot_ThrowsOrRejects()
    {
        // Spec implies variables do not support namespaces (unlike verbs)
        // Lexer also rejects dots.
        Assert.ThrowsAny<Exception>(() => _store.Set("ns.var", new ZohInt(1)));
    }
}
