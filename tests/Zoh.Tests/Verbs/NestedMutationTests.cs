using System.Collections.Immutable;
using Xunit;
using Zoh.Runtime.Execution;
using Zoh.Runtime.Parsing.Ast;
using Zoh.Runtime.Types;
using Zoh.Runtime.Variables;
using Zoh.Runtime.Verbs;
using Zoh.Runtime.Verbs.Core;
using Zoh.Runtime.Helpers;
using Zoh.Runtime.Storage;
using Zoh.Runtime.Lexing;

namespace Zoh.Tests.Verbs
{
    public class NestedMutationTests
    {
        private readonly VariableStore _variables;
        private readonly Context _context;

        public NestedMutationTests()
        {
            _variables = new VariableStore(new Dictionary<string, Variable>());
            _context = new Context(_variables, new InMemoryStorage(), new ChannelManager());
            _context.VerbExecutor = (v, c) => VerbResult.Ok(); // Mock
        }

        private static TextPosition Ps() => new TextPosition(1, 1, 0);

        private VerbCallAst MakeVerbCall(string name, params ValueAst[] args)
        {
            return new VerbCallAst(
                "core", name, false, [],
                ImmutableDictionary<string, ValueAst>.Empty,
                [.. args],
                Ps());
        }

        // Helper to create nested reference *var["path"]["path2"]
        private ValueAst.Reference MakeNestedRef(string name, params ValueAst[] path)
        {
            return new ValueAst.Reference(name, path.ToImmutableArray());
        }

        [Fact]
        public void TestAppendNested()
        {
            // Setup: *data = {"list": [1, 2]}
            var initialList = new ZohList(ImmutableArray.Create<ZohValue>(new ZohInt(1), new ZohInt(2)));
            var initialMap = new ZohMap(ImmutableDictionary<string, ZohValue>.Empty.Add("list", initialList));
            _variables.Set("data", initialMap);

            // Execute: /append *data["list"], 3;
            var driver = new AppendDriver();
            var verb = MakeVerbCall("append",
                MakeNestedRef("data", new ValueAst.String("list")),
                new ValueAst.Integer(3)
            );

            var result = driver.Execute(_context, verb);

            // Verify
            Assert.False(result.IsFatal);
            var data = _variables.Get("data");
            Assert.IsType<ZohMap>(data);
            var innerList = CollectionHelpers.GetAtPath(_context, "data", ImmutableArray.Create<ValueAst>(new ValueAst.String("list")));
            Assert.IsType<ZohList>(innerList);
            Assert.Equal(3, ((ZohList)innerList).Items.Length);
            Assert.Equal(new ZohInt(3), ((ZohList)innerList).Items[2]);
        }

        [Fact]
        public void TestInsertNested()
        {
            // Setup: *data = {"list": [1, 3]}
            var initialList = new ZohList(ImmutableArray.Create<ZohValue>(new ZohInt(1), new ZohInt(3)));
            var initialMap = new ZohMap(ImmutableDictionary<string, ZohValue>.Empty.Add("list", initialList));
            _variables.Set("data", initialMap);

            // Execute: /insert *data["list"], 1, 2;
            var driver = new InsertDriver();
            var verb = MakeVerbCall("insert",
                MakeNestedRef("data", new ValueAst.String("list")),
                new ValueAst.Integer(1),
                new ValueAst.Integer(2)
            );

            var result = driver.Execute(_context, verb);

            // Verify
            Assert.False(result.IsFatal);
            var innerList = CollectionHelpers.GetAtPath(_context, "data", ImmutableArray.Create<ValueAst>(new ValueAst.String("list")));
            Assert.IsType<ZohList>(innerList);
            Assert.Equal(3, ((ZohList)innerList).Items.Length);
            Assert.Equal(new ZohInt(2), ((ZohList)innerList).Items[1]);
        }

        [Fact]
        public void TestRemoveNestedList()
        {
            // Setup: *data = {"list": [1, 2, 3]}
            var initialList = new ZohList(ImmutableArray.Create<ZohValue>(new ZohInt(1), new ZohInt(2), new ZohInt(3)));
            var initialMap = new ZohMap(ImmutableDictionary<string, ZohValue>.Empty.Add("list", initialList));
            _variables.Set("data", initialMap);

            // Execute: /remove *data["list"], 1;
            var driver = new RemoveDriver();
            var verb = MakeVerbCall("remove",
                MakeNestedRef("data", new ValueAst.String("list")),
                new ValueAst.Integer(1)
            );

            var result = driver.Execute(_context, verb);

            // Verify
            Assert.False(result.IsFatal);
            var innerList = CollectionHelpers.GetAtPath(_context, "data", ImmutableArray.Create<ValueAst>(new ValueAst.String("list")));
            Assert.Equal(2, ((ZohList)innerList).Items.Length);
            Assert.Equal(new ZohInt(3), ((ZohList)innerList).Items[1]);
        }

        [Fact]
        public void TestRemoveNestedMap()
        {
            // Setup: *data = {"nested": {"a": 1, "b": 2}}
            var innerMap = new ZohMap(ImmutableDictionary<string, ZohValue>.Empty.Add("a", new ZohInt(1)).Add("b", new ZohInt(2)));
            var outerMap = new ZohMap(ImmutableDictionary<string, ZohValue>.Empty.Add("nested", innerMap));
            _variables.Set("data", outerMap);

            // Execute: /remove *data["nested"], "a";
            var driver = new RemoveDriver();
            var verb = MakeVerbCall("remove",
                MakeNestedRef("data", new ValueAst.String("nested")),
                new ValueAst.String("a")
            );

            var result = driver.Execute(_context, verb);

            // Verify
            Assert.False(result.IsFatal);
            var nested = CollectionHelpers.GetAtPath(_context, "data", ImmutableArray.Create<ValueAst>(new ValueAst.String("nested")));
            Assert.False(((ZohMap)nested).Items.ContainsKey("a"));
            Assert.True(((ZohMap)nested).Items.ContainsKey("b"));
        }

        [Fact]
        public void TestClearNested()
        {
            // Setup: *data = {"list": [1, 2]}
            var initialList = new ZohList(ImmutableArray.Create<ZohValue>(new ZohInt(1), new ZohInt(2)));
            var initialMap = new ZohMap(ImmutableDictionary<string, ZohValue>.Empty.Add("list", initialList));
            _variables.Set("data", initialMap);

            // Execute: /clear *data["list"];
            var driver = new ClearDriver();
            var verb = MakeVerbCall("clear",
                MakeNestedRef("data", new ValueAst.String("list"))
            );

            var result = driver.Execute(_context, verb);

            // Verify
            Assert.False(result.IsFatal);
            var innerList = CollectionHelpers.GetAtPath(_context, "data", ImmutableArray.Create<ValueAst>(new ValueAst.String("list")));
            Assert.True(((ZohList)innerList).Items.IsEmpty);
        }

        [Fact]
        public void TestDeeplyNested()
        {
            // Setup: *data = {"users": [{"items": []}]}
            var itemsList = new ZohList(ImmutableArray<ZohValue>.Empty);
            var userMap = new ZohMap(ImmutableDictionary<string, ZohValue>.Empty.Add("items", itemsList));
            var usersList = new ZohList(ImmutableArray.Create<ZohValue>(userMap));
            var dataMap = new ZohMap(ImmutableDictionary<string, ZohValue>.Empty.Add("users", usersList));
            _variables.Set("data", dataMap);

            // Execute: /append *data["users"][0]["items"], "sword";
            var driver = new AppendDriver();
            var verb = MakeVerbCall("append",
                MakeNestedRef("data",
                    new ValueAst.String("users"),
                    new ValueAst.Integer(0),
                    new ValueAst.String("items")
                ),
                new ValueAst.String("sword")
            );

            var result = driver.Execute(_context, verb);

            // Verify
            Assert.False(result.IsFatal);
            var updatedData = (ZohMap)_variables.Get("data");
            var uList = (ZohList)updatedData.Items["users"];
            var uMap = (ZohMap)uList.Items[0];
            var iList = (ZohList)uMap.Items["items"];
            Assert.Single(iList.Items);
            Assert.Equal(new ZohStr("sword"), iList.Items[0]);
        }
    }
}
