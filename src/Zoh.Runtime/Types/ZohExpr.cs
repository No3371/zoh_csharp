using Zoh.Runtime.Expressions;
using Zoh.Runtime.Parsing.Ast;

namespace Zoh.Runtime.Types;

public sealed record ZohExpr(ValueAst.Expression ast) : ZohValue
{
    public override ZohValueType Type => ZohValueType.Expression;
    public override string ToString() => $"`{ast.Source}`";
}
