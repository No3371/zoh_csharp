using Xunit;
using Zoh.Runtime.Types;

namespace Zoh.Tests.Types;

public class TypeCoercionTests
{
    [Fact]
    public void IsTruthy_Rules()
    {
        // Nothing -> False
        Assert.False(ZohValue.Nothing.IsTruthy());

        // Bool
        Assert.True(new ZohBool(true).IsTruthy());
        Assert.False(new ZohBool(false).IsTruthy());

        // Int: 0 -> False, else True
        Assert.False(new ZohInt(0).IsTruthy());
        Assert.True(new ZohInt(1).IsTruthy());
        Assert.True(new ZohInt(-1).IsTruthy());

        // Float: 0.0 -> False, else True
        Assert.False(new ZohFloat(0.0).IsTruthy());
        Assert.True(new ZohFloat(0.1).IsTruthy());
        Assert.True(new ZohFloat(-0.001).IsTruthy());

        // Str: Empty -> False, else True
        Assert.False(new ZohStr("").IsTruthy());
        Assert.True(new ZohStr(" ").IsTruthy()); // Space is true? Spec says non-empty.
        Assert.True(new ZohStr("hello").IsTruthy());

        // List: Empty -> False, else True
        Assert.False(new ZohList([]).IsTruthy());
        Assert.True(new ZohList([new ZohInt(1)]).IsTruthy());

        // Map: Empty -> False, else True
        Assert.False(new ZohMap(System.Collections.Immutable.ImmutableDictionary<string, ZohValue>.Empty).IsTruthy());

        var map = System.Collections.Immutable.ImmutableDictionary<string, ZohValue>.Empty.Add("k", new ZohInt(1));
        Assert.True(new ZohMap(map).IsTruthy());
    }

    [Fact]
    public void AsInt_Conversions()
    {
        // Int -> Int
        Assert.Equal(new ZohInt(42), new ZohInt(42).AsInt());

        // Float -> Int (Truncate toward zero per spec)
        Assert.Equal(new ZohInt(42), new ZohFloat(42.9).AsInt());
        Assert.Equal(new ZohInt(-42), new ZohFloat(-42.9).AsInt()); // Truncate toward zero, not floor

        // Str -> Int (Parse)
        Assert.Equal(new ZohInt(42), new ZohStr("42").AsInt());

        // Str -> Invalid
        Assert.Throws<InvalidCastException>(() => new ZohStr("abc").AsInt());
    }

    [Fact]
    public void AsFloat_Conversions()
    {
        // Float -> Float
        Assert.Equal(new ZohFloat(42.5), new ZohFloat(42.5).AsFloat());

        // Int -> Float
        Assert.Equal(new ZohFloat(42.0), new ZohInt(42).AsFloat());

        // Bool -> Float
        Assert.Equal(new ZohFloat(1.0), new ZohBool(true).AsFloat());
        Assert.Equal(new ZohFloat(0.0), new ZohBool(false).AsFloat());

        // Str -> Float (Parse)
        Assert.Equal(new ZohFloat(42.5), new ZohStr("42.5").AsFloat());

        // Str -> Invalid
        Assert.Throws<InvalidCastException>(() => new ZohStr("abc").AsFloat());
    }

    [Fact]
    public void AsString_Conversions()
    {
        // Str -> Str
        Assert.Equal(new ZohStr("hello"), new ZohStr("hello").AsString());

        // Nothing -> "?"
        Assert.Equal(new ZohStr("?"), ZohValue.Nothing.AsString());

        // Bool -> "true"/"false"
        Assert.Equal(new ZohStr("true"), new ZohBool(true).AsString());
        Assert.Equal(new ZohStr("false"), new ZohBool(false).AsString());

        // Int -> string
        Assert.Equal(new ZohStr("42"), new ZohInt(42).AsString());

        // Float -> string (InvariantCulture)
        Assert.Equal(new ZohStr("42.5"), new ZohFloat(42.5).AsString());

        // List/Map -> ToString?
        var list = new ZohList([new ZohInt(1)]);
        Assert.NotNull(list.AsString().Value);
    }
}
