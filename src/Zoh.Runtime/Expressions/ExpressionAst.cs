using Zoh.Runtime.Types;
using Zoh.Runtime.Lexing;
using Zoh.Runtime.Parsing.Ast;

namespace Zoh.Runtime.Expressions;

public abstract record ExpressionAst;

public sealed record LiteralExpressionAst(ZohValue Value) : ExpressionAst;

public sealed record VariableExpressionAst(string Name, ExpressionAst? Index = null) : ExpressionAst;

public sealed record UnaryExpressionAst(TokenType Operator, ExpressionAst Operand) : ExpressionAst;

public sealed record BinaryExpressionAst(ExpressionAst Left, TokenType Operator, ExpressionAst Right) : ExpressionAst;

public sealed record GroupingExpressionAst(ExpressionAst Expression) : ExpressionAst;

public sealed record InterpolateExpressionAst(ExpressionAst Expression) : ExpressionAst;
public sealed record CountExpressionAst(ExpressionAst Reference) : ExpressionAst;
public sealed record ConditionalExpressionAst(ExpressionAst Condition, ExpressionAst Then, ExpressionAst Else) : ExpressionAst;
public sealed record AnyExpressionAst(System.Collections.Immutable.ImmutableArray<ExpressionAst> Options) : ExpressionAst;
public sealed record IndexedExpressionAst(System.Collections.Immutable.ImmutableArray<ExpressionAst> Options, ExpressionAst Index, bool Wrap) : ExpressionAst;
public sealed record RollExpressionAst(System.Collections.Immutable.ImmutableArray<ExpressionAst> Options) : ExpressionAst;
public sealed record WeightedRollExpressionAst(System.Collections.Immutable.ImmutableArray<(ExpressionAst Option, int Weight)> Options) : ExpressionAst;

