using System.Collections.Immutable;
using Zoh.Runtime.Types;

namespace Zoh.Tests.Types;

public class ZohValueTests
{
    [Fact]
    public void Truthiness_FollowsSpec()
    {
        Assert.False(ZohValue.Nothing.IsTruthy());
        Assert.False(ZohValue.False.IsTruthy());
        Assert.True(ZohValue.True.IsTruthy());
        Assert.False(new ZohInt(0).IsTruthy());
        Assert.True(new ZohInt(1).IsTruthy());
        Assert.False(new ZohFloat(0.0).IsTruthy());
        Assert.True(new ZohFloat(0.1).IsTruthy());
        Assert.False(new ZohStr("").IsTruthy());
        Assert.True(new ZohStr(" ").IsTruthy()); // Space is truthy
        Assert.False(new ZohList([]).IsTruthy());
        Assert.True(new ZohList([ZohValue.Nothing]).IsTruthy());
    }

    [Fact]
    public void List_DeepEquality()
    {
        var list1 = new ZohList([new ZohInt(1), new ZohStr("a")]);
        var list2 = new ZohList([new ZohInt(1), new ZohStr("a")]);
        var list3 = new ZohList([new ZohInt(1), new ZohStr("b")]);

        Assert.Equal(list1, list2);
        Assert.NotEqual(list1, list3);
    }

    [Fact]
    public void Map_DeepEquality()
    {
        var map1 = new ZohMap(ImmutableDictionary<string, ZohValue>.Empty.Add("k", new ZohInt(1)));
        var map2 = new ZohMap(ImmutableDictionary<string, ZohValue>.Empty.Add("k", new ZohInt(1)));
        var map3 = new ZohMap(ImmutableDictionary<string, ZohValue>.Empty.Add("k", new ZohInt(2)));

        Assert.Equal(map1, map2);
        Assert.NotEqual(map1, map3);
    }

    [Fact]
    public void String_Equality_IsCaseSensitive()
    {
        var s1 = new ZohStr("Hello");
        var s2 = new ZohStr("hello");
        Assert.NotEqual(s1, s2);
    }

    [Fact]
    public void DeepClone_CopiesNestedStructures()
    {
        // List<Map<...>>
        var innerMap = new ZohMap(ImmutableDictionary<string, ZohValue>.Empty.Add("k", new ZohInt(1)));
        var list = new ZohList([innerMap]);

        var clone = (ZohList)list.DeepClone();

        Assert.Equal(list, clone); // Should be equal by value
        Assert.NotSame(list, clone); // Should be different instances
        Assert.NotSame(list.Items[0], clone.Items[0]); // Inner map should be different instance (Deep Copy)
    }
}
