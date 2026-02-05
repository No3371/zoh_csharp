using Zoh.Runtime.Types;
using Zoh.Runtime.Parsing.Ast;
using Zoh.Runtime.Parsing;
using Zoh.Runtime.Variables;
using System.Collections.Immutable;
using System.Linq;
using Zoh.Runtime.Lexing;
using System.Diagnostics;

namespace Zoh.Runtime.Expressions;

public class ExpressionEvaluator
{
    private readonly VariableStore _variables;

    public ExpressionEvaluator(VariableStore variables)
    {
        _variables = variables;
    }

    public ZohValue Evaluate(ExpressionAst ast) => ast switch
    {
        LiteralExpressionAst l => l.Value,
        VariableExpressionAst v => EvaluateVariable(v),
        UnaryExpressionAst u => EvaluateUnary(u),
        BinaryExpressionAst b => EvaluateBinary(b),
        GroupingExpressionAst g => Evaluate(g.Expression),
        CountExpressionAst c => EvaluateCount(c),
        ConditionalExpressionAst cond => EvaluateConditional(cond),
        AnyExpressionAst a => EvaluateAny(a),
        IndexedExpressionAst i => EvaluateIndexed(i),
        RollExpressionAst r => EvaluateRoll(r),
        InterpolateExpressionAst interp => EvaluateInterpolate(interp),
        _ => throw new NotImplementedException($"Unknown AST node: {ast.GetType().Name}")
    };

    private ZohValue EvaluateCount(CountExpressionAst c)
    {
        var val = Evaluate(c.Reference);
        if (val == ZohValue.Nothing) return new ZohInt(0);
        return val switch
        {
            ZohStr s => new ZohInt(s.Value.Length),
            ZohList l => new ZohInt(l.Items.Length),
            IZohMap m => new ZohInt(m.Count),
            // Channel?
            _ => throw new InvalidOperationException($"Cannot count type {val.Type}")
        };
    }

    private ZohValue EvaluateConditional(ConditionalExpressionAst c)
    {
        var cond = Evaluate(c.Condition);
        if (cond.IsTruthy()) return Evaluate(c.Then);
        return Evaluate(c.Else);
    }

    private ZohValue EvaluateVariable(VariableExpressionAst v)
    {
        if (!_variables.TryGet(v.Name, out var value))
        {
            throw new InvalidOperationException($"Undefined variable: {v.Name}");
        }

        if (v.Index == null) return value;

        // Handle indexed access
        var index = Evaluate(v.Index);

        return value switch
        {
            ZohList list => GetListItem(list, index),
            IZohMap map => GetMapItem(map, index),
            ZohStr str => GetStringChar(str, index),
            _ => throw new InvalidOperationException($"Cannot index into type {value.Type}")
        };
    }

    private static ZohValue GetListItem(ZohList list, ZohValue index)
    {
        if (index is not ZohInt i)
            throw new InvalidOperationException("List index must be an integer");

        var idx = (int)i.Value;
        if (idx < 0) idx = list.Items.Length + idx; // Negative indexing

        if (idx < 0 || idx >= list.Items.Length)
            throw new IndexOutOfRangeException($"Index {i.Value} out of bounds for list of size {list.Items.Length}");

        return list.Items[idx];
    }

    private static ZohValue GetMapItem(IZohMap map, ZohValue index)
    {
        var key = index.ToString();
        return map.TryGet(key, out var val) ? val : ZohValue.Nothing;
    }

    private static ZohValue GetStringChar(ZohStr str, ZohValue index)
    {
        if (index is not ZohInt i)
            throw new InvalidOperationException("String index must be an integer");

        var idx = (int)i.Value;
        if (idx < 0) idx = str.Value.Length + idx;

        if (idx < 0 || idx >= str.Value.Length)
            throw new IndexOutOfRangeException($"Index {i.Value} out of bounds for string of length {str.Value.Length}");

        return new ZohStr(str.Value[idx].ToString());
    }


    private ZohValue EvaluateAny(AnyExpressionAst a)
    {
        foreach (var opt in a.Options)
        {
            try
            {
                var val = Evaluate(opt);
                if (val != ZohValue.Nothing) return val;
            }
            catch (InvalidOperationException ex) when (ex.Message.StartsWith("Undefined variable"))
            {
                // Spec Ambiguity: Should Any swallow undefined vars?
                // "Return first non-Nothing". Undefined is Error.
                // If we want Any to work like "Coalesce", it usually handles nulls (Nothing).
                // Undefined variable is strict error.
                // Re-throw.
                throw;
            }
        }
        return ZohValue.Nothing;
    }

    private ZohValue EvaluateIndexed(IndexedExpressionAst i)
    {
        var options = i.Options.Select(Evaluate).ToImmutableArray();
        if (options.Length == 0) return ZohValue.Nothing;

        var indexVal = Evaluate(i.Index);
        if (indexVal is not ZohInt idx) throw new InvalidOperationException("Index must be an integer");

        int index = (int)idx.Value;
        int length = options.Length;

        if (i.Wrap)
        {
            // Wrap mode: modulo arithmetic, always valid index
            index = ((index % length) + length) % length;
        }
        else
        {
            // Standard negative indexing: -1 = last, -2 = second-to-last, etc.
            if (index < 0) index += length;

            // Bounds check (still throws if out of range)
            if (index < 0 || index >= length)
                throw new IndexOutOfRangeException($"Index {(int)idx.Value} out of bounds for option list of size {length}");
        }

        return options[index];
    }

    private ZohValue EvaluateRoll(RollExpressionAst r)
    {
        var options = r.Options.Select(Evaluate).ToImmutableArray();
        if (options.Length == 0) return ZohValue.Nothing;

        var rand = new Random();
        return options[rand.Next(options.Length)];
    }

    private ZohValue EvaluateInterpolate(InterpolateExpressionAst i)
    {
        var val = Evaluate(i.Expression);
        var str = val.ToString();

        // Use ScannerUtility to parse nested expressions in the string
        var sb = new System.Text.StringBuilder();
        var index = 0;

        while (index < str.Length)
        {
            // Check for escape sequence \{ or \}
            if (str[index] == '\\' && index + 1 < str.Length)
            {
                var next = str[index + 1];
                if (next == '{' || next == '}')
                {
                    sb.Append(next);
                    index += 2;
                    continue;
                }
            }

            var result = ScannerUtility.ScanPattern(str, index, '{', '}');
            if (result.Success)
            {
                var match = result.Match!;
                var matchResult = EvaluateInterpolationMatch(match);
                sb.Append(matchResult.ToString());
                index += match.FullText.Length;
            }
            else if (result.Error != null)
            {
                throw new Exception($"invalid_syntax: {result.Error}");
            }
            else
            {
                sb.Append(str[index]);
                index++;
            }
        }

        return new ZohStr(sb.ToString());
    }

    private ZohValue EvaluateInterpolationMatch(MatchResult match)
    {
        string exprSource;

        if (match.OpenToken == "${")
        {
            var parts = match.Content.Split("...", 2, StringSplitOptions.None);
            if (parts.Length == 2)
            {
                var collectionExpr = parts[0].Trim();
                var delimExpr = parts[1].Trim();
                var colVal = EvaluateExprString(collectionExpr);
                var delimVal = EvaluateExprString(delimExpr);

                if (colVal is ZohList list)
                {
                    return new ZohStr(string.Join(delimVal.ToString(), list.Items.Select(x => x.ToString())));
                }
                return colVal;
            }
            exprSource = match.Content;
        }
        else if (match.OpenToken == "$#{") exprSource = "$#(" + match.Content + ")";
        else if (match.OpenToken == "$?{") exprSource = "$?(" + match.Content + ")";
        else throw new Exception("Unknown token " + match.OpenToken);

        if (!string.IsNullOrEmpty(match.Suffix))
        {
            exprSource = "$(" + exprSource + ")" + match.Suffix;
        }

        return EvaluateExprString(exprSource);
    }

    private ZohValue EvaluateExprString(string source)
    {
        var lexer = new Lexer(source);
        var result = lexer.Tokenize();
        if (result.Errors.Length > 0)
            throw new Exception("Lexer error: " + result.Errors[0].Message);

        var parser = new ExpressionParser(result.Tokens);
        var ast = parser.Parse();
        return Evaluate(ast);
    }

    private ZohValue EvaluateUnary(UnaryExpressionAst u)
    {
        var operand = Evaluate(u.Operand);
        switch (u.Operator)
        {
            case TokenType.Bang:
                return ZohValue.FromBool(!operand.IsTruthy());
            case TokenType.Minus:
                if (operand is ZohInt i) return new ZohInt(-i.Value);
                if (operand is ZohFloat f) return new ZohFloat(-f.Value);
                throw new InvalidOperationException($"Cannot apply unary minus to {operand.Type}");
            default:
                throw new InvalidOperationException($"Unknown unary operator {u.Operator}");
        }
    }

    private ZohValue EvaluateBinary(BinaryExpressionAst b)
    {
        // Logical operators (Short-circuit)
        if (b.Operator == TokenType.PipePipe)
        {
            var leftVal = Evaluate(b.Left);
            if (leftVal.IsTruthy()) return leftVal;
            return Evaluate(b.Right);
        }
        if (b.Operator == TokenType.AmpersandAmpersand)
        {
            var leftVal = Evaluate(b.Left);
            if (!leftVal.IsTruthy()) return leftVal;
            return Evaluate(b.Right);
        }

        var left = Evaluate(b.Left);
        var right = Evaluate(b.Right);

        switch (b.Operator)
        {
            // Arithmetic
            case TokenType.Plus:
                if (left is ZohStr || right is ZohStr)
                    return new ZohStr(left.ToString() + right.ToString());
                if (left is ZohInt li && right is ZohInt ri)
                    return new ZohInt(li.Value + ri.Value);
                if (left is ZohFloat || right is ZohFloat)
                {
                    double ld = left is ZohInt lii ? lii.Value : ((ZohFloat)left).Value;
                    double rd = right is ZohInt rii ? rii.Value : ((ZohFloat)right).Value;
                    return new ZohFloat(ld + rd);
                }
                throw new InvalidOperationException($"Cannot apply + to {left.Type} and {right.Type}");

            case TokenType.Minus:
            case TokenType.Star:
            case TokenType.Slash:
            case TokenType.Percent:
            case TokenType.StarStar:
                return EvaluateNumeric(left, right, b.Operator);

            // Comparison
            case TokenType.EqualEqual:
                return ZohValue.FromBool(left.Equals(right));
            case TokenType.BangEqual:
                return ZohValue.FromBool(!left.Equals(right));

            case TokenType.LeftAngle:
            case TokenType.LessEqual:
            case TokenType.RightAngle:
            case TokenType.GreaterEqual:
                return EvaluateComparison(left, right, b.Operator);

            default:
                throw new InvalidOperationException($"Unknown binary operator {b.Operator}");
        }
    }

    private ZohValue EvaluateNumeric(ZohValue left, ZohValue right, TokenType op)
    {
        if (left is ZohInt li && right is ZohInt ri)
        {
            return op switch
            {
                TokenType.Minus => new ZohInt(li.Value - ri.Value),
                TokenType.Star => new ZohInt(li.Value * ri.Value),
                TokenType.Slash => new ZohInt(li.Value / ri.Value), // Integer division? Or float? Spec typically says ZohInt division is integer division?
                // Spec say: "Division of integers results in integer".
                TokenType.Percent => new ZohInt(li.Value % ri.Value),
                TokenType.StarStar => EvaluateIntPower(li.Value, ri.Value),
                _ => throw new InvalidOperationException()
            };
        }

        // Float promotion
        double ld = left is ZohInt lii ? lii.Value : (left is ZohFloat lf ? lf.Value : throw new InvalidOperationException($"Operand must be number: {left}"));
        double rd = right is ZohInt rii ? rii.Value : (right is ZohFloat rf ? rf.Value : throw new InvalidOperationException($"Operand must be number: {right}"));

        return op switch
        {
            TokenType.Minus => new ZohFloat(ld - rd),
            TokenType.Star => new ZohFloat(ld * rd),
            TokenType.Slash => new ZohFloat(ld / rd),
            TokenType.Percent => new ZohFloat(ld % rd),
            TokenType.StarStar => EvaluateFloatPower(ld, rd),
            _ => throw new InvalidOperationException()
        };
    }

    private static ZohValue EvaluateIntPower(long baseVal, long expVal)
    {
        if (expVal < 0) return new ZohFloat(Math.Pow(baseVal, expVal));

        double res = Math.Pow(baseVal, expVal);
        if (double.IsInfinity(res) || res >= long.MaxValue || res < long.MinValue)
            return new ZohFloat(res);

        return new ZohInt((long)res);
    }

    private static ZohValue EvaluateFloatPower(double baseVal, double expVal)
    {
        return new ZohFloat(Math.Pow(baseVal, expVal));
    }

    private ZohBool EvaluateComparison(ZohValue left, ZohValue right, TokenType op)
    {
        int compare = 0;
        if (left is ZohInt li && right is ZohInt ri) compare = li.Value.CompareTo(ri.Value);
        else if (left is ZohStr ls && right is ZohStr rs) compare = string.Compare(ls.Value, rs.Value, StringComparison.Ordinal); // Strict
        else
        {
            // Float comparison
            double ld = left is ZohInt lii ? lii.Value : (left is ZohFloat lf ? lf.Value : double.NaN);
            double rd = right is ZohInt rii ? rii.Value : (right is ZohFloat rf ? rf.Value : double.NaN);

            if (double.IsNaN(ld) || double.IsNaN(rd)) throw new InvalidOperationException($"Cannot compare {left.Type} and {right.Type}");
            compare = ld.CompareTo(rd);
        }

        return op switch
        {
            TokenType.LeftAngle => ZohValue.FromBool(compare < 0),
            TokenType.LessEqual => ZohValue.FromBool(compare <= 0),
            TokenType.RightAngle => ZohValue.FromBool(compare > 0),
            TokenType.GreaterEqual => ZohValue.FromBool(compare >= 0),
            _ => ZohValue.False
        };
    }
}
