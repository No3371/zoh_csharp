using Xunit;
using Zoh.Runtime.Execution;
using Zoh.Runtime.Types;
using Zoh.Runtime.Parsing.Ast;
using Zoh.Tests.Execution;

namespace Zoh.Tests.Runtime;

public class ReproTests
{
    private readonly TestExecutionContext _context;

    public ReproTests()
    {
        _context = new TestExecutionContext();
    }

    [Fact]
    public void ZohStr_Equality_Works()
    {
        var s1 = new ZohStr("b");
        var s2 = new ZohStr("b");
        Assert.Equal(s1, s2);
        Assert.True(s1.Equals(s2));
    }

    [Fact]
    public void Reference_ResolvesTo_ZohStr()
    {
        _context.Variables.Set("val", new ZohStr("b"));
        var refAst = new ValueAst.Reference("val");
        var resolved = ValueResolver.Resolve(refAst, _context);

        Assert.IsType<ZohStr>(resolved);
        Assert.Equal("b", ((ZohStr)resolved).Value);
        Assert.Equal(new ZohStr("b"), resolved);
    }
}
