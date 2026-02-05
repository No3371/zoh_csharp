using System.Collections.Immutable;
using Zoh.Runtime.Lexing;
using Zoh.Runtime.Types; // For ZohValue factories
using Zoh.Runtime.Parsing.Ast;
using System.Text;

namespace Zoh.Runtime.Expressions;

public class ExpressionParser(ImmutableArray<Token> tokens)
{
    private int _current = 0;

    private Token Peek(int offset = 0) =>
        _current + offset < tokens.Length ? tokens[_current + offset] : Token.Eof(new TextPosition(0, 0, 0));

    private ReadOnlySpan<Token> Peek(int offset, int length) =>
        _current + offset < tokens.Length ? tokens.AsSpan().Slice(_current + offset, Math.Min(length, tokens.Length - _current - offset)) : Span<Token>.Empty;

    private Token Current => Peek();
    private Token Advance()
    {
        if (_current < tokens.Length) _current++;
        return Peek(-1);
    }

    private bool Match(TokenType type)
    {
        if (Current.Type == type)
        {
            Advance();
            return true;
        }
        return false;
    }

    public ExpressionAst Parse()
    {
        if (tokens.Length == 0) return new LiteralExpressionAst(ZohValue.Nothing);
        return ParseLogicalOr();
    }

    private ExpressionAst ParseLogicalOr()
    {
        var left = ParseLogicalAnd();

        while (Match(TokenType.PipePipe))
        {
            var op = TokenType.PipePipe;
            var right = ParseLogicalAnd();
            left = new BinaryExpressionAst(left, op, right);
        }

        return left;
    }

    private ExpressionAst ParseLogicalAnd()
    {
        var left = ParseEquality();

        while (Match(TokenType.AmpersandAmpersand))
        {
            var op = TokenType.AmpersandAmpersand;
            var right = ParseEquality();
            left = new BinaryExpressionAst(left, op, right);
        }

        return left;
    }

    private ExpressionAst ParseEquality()
    {
        var left = ParseComparison();

        while (Current.Type == TokenType.EqualEqual || Current.Type == TokenType.BangEqual)
        {
            var op = Advance().Type;
            var right = ParseComparison();
            left = new BinaryExpressionAst(left, op, right);
        }

        return left;
    }

    private ExpressionAst ParseComparison()
    {
        var left = ParseTerm();

        while (Current.Type == TokenType.LeftAngle || Current.Type == TokenType.LessEqual ||
               Current.Type == TokenType.RightAngle || Current.Type == TokenType.GreaterEqual)
        {
            var op = Advance().Type;
            var right = ParseTerm();
            left = new BinaryExpressionAst(left, op, right);
        }

        return left;
    }

    private ExpressionAst ParseTerm()
    {
        var left = ParseFactor();

        while (Current.Type == TokenType.Plus || Current.Type == TokenType.Minus)
        {
            var op = Advance().Type;
            var right = ParseFactor();
            left = new BinaryExpressionAst(left, op, right);
        }

        return left;
    }

    private ExpressionAst ParseFactor()
    {
        var left = ParseUnary();

        while (Current.Type == TokenType.Star || Current.Type == TokenType.Slash || Current.Type == TokenType.Percent)
        {
            var op = Advance().Type;
            var right = ParseUnary();
            left = new BinaryExpressionAst(left, op, right);
        }

        return left;
    }

    private ExpressionAst ParseUnary()
    {
        if (Current.Type == TokenType.Bang || Current.Type == TokenType.Minus)
        {
            var op = Advance().Type;
            var right = ParseUnary();
            return new UnaryExpressionAst(op, right);
        }

        return ParsePower();
    }

    private ExpressionAst ParsePower()
    {
        var left = ParsePrimary();

        if (Match(TokenType.StarStar))
        {
            var op = TokenType.StarStar;
            var right = ParseUnary();
            return new BinaryExpressionAst(left, op, right);
        }

        return left;
    }

    private ExpressionAst ParsePrimary()
    {
        if (Match(TokenType.False)) return new LiteralExpressionAst(ZohValue.False);
        if (Match(TokenType.True)) return new LiteralExpressionAst(ZohValue.True);
        if (Match(TokenType.Nothing)) return new LiteralExpressionAst(ZohValue.Nothing);

        if (Current.Type == TokenType.Integer)
        {
            var val = (long)Advance().Value!;
            return new LiteralExpressionAst(new ZohInt(val));
        }
        if (Current.Type == TokenType.Double)
        {
            var val = (double)Advance().Value!;
            return new LiteralExpressionAst(new ZohFloat(val));
        }
        if (Current.Type == TokenType.String)
        {
            var val = (string)Advance().Value!;
            return new LiteralExpressionAst(new ZohStr(val));
        }

        if (Match(TokenType.Star))
        {
            return ParseReference();
        }

        // Grouping
        if (Match(TokenType.LeftParen))
        {
            var expr = ParseLogicalOr();
            if (!Match(TokenType.RightParen))
            {
                throw new Exception($"Expected ')' at {Current.Start}");
            }
            return new GroupingExpressionAst(expr);
        }

        if (Match(TokenType.DollarString))
        {
            var str = Consume(TokenType.String, "Expected string after $\"").Value;
            // Create a literal AST for the string, then wrap in Interpolate
            return new InterpolateExpressionAst(new LiteralExpressionAst(new ZohStr((string)str!)));
        }
        if (Match(TokenType.DollarRef))
        {
            // $* consumed. ParseReference expects Identifier next.
            var variable = ParseReference();
            return new InterpolateExpressionAst(variable);
        }

        if (Current.Type == TokenType.DollarParen) return ParseSpecialDollarParen();
        if (Current.Type == TokenType.DollarHashParen) return ParseCount();
        if (Current.Type == TokenType.DollarQuestionParen) return ParseConditionalOrAny();

        throw new Exception($"Unexpected token {Current.Type} at {Current.Start}");
    }

    private Token Consume(TokenType type, string message)
    {
        if (Check(type)) return Advance();
        throw new Exception(message + $" at {Current.Start}");
    }

    private bool Check(TokenType type) => _current < tokens.Length && Current.Type == type;

    private string ParseNamespacedIdentifier(string errorMessage)
    {
        var sb = new StringBuilder();
        sb.Append(Consume(TokenType.Identifier, errorMessage).Lexeme);

        while (Match(TokenType.Dot))
        {
            sb.Append('.');
            sb.Append(Consume(TokenType.Identifier, "Expected identifier after '.'").Lexeme);
        }
        return sb.ToString();
    }



    private VariableExpressionAst ParseReference()
    {
        // Parse *identifier[index]? where * is already consumed
        if (Current.Type != TokenType.Identifier)
        {
            throw new Exception($"Expected identifier after '*' at {Current.Start}");
        }
        var name = ParseNamespacedIdentifier("Expected identifier after '*'");

        // Optional index access
        ExpressionAst? index = null;
        if (Match(TokenType.LeftBracket))
        {
            index = ParseLogicalOr();
            Consume(TokenType.RightBracket, "Expected ']' after index");
        }

        return new VariableExpressionAst(name, index);
    }

    private ExpressionAst ParseSpecialDollarParen()
    {
        Advance(); // Consume $(
        var first = ParseLogicalOr();

        var options = new List<ExpressionAst> { first };

        while (Match(TokenType.Pipe))
        {
            options.Add(ParseLogicalOr());
        }

        Consume(TokenType.RightParen, "Expected ')' after $(...)");

        if (Check(TokenType.LeftBracket))
        {
            Advance(); // Consume [
            if (Match(TokenType.Percent))
            {
                Consume(TokenType.RightBracket, "Expected ']' after %");
                return new RollExpressionAst(options.ToImmutableArray());
            }

            var wrap = Match(TokenType.Bang);
            var index = ParsePrimary();
            Consume(TokenType.RightBracket, "Expected ']' after index");
            return new IndexedExpressionAst(options.ToImmutableArray(), index, wrap);
        }

        // $(expr) without suffix is now just a single-option Any/List, no longer Interpolate.
        // It simply groups or selects first non-nothing (Any behavior).
        return new AnyExpressionAst(options.ToImmutableArray());
    }

    private ExpressionAst ParseCount()
    {
        Advance(); // Consume $#(
        var expr = ParseLogicalOr();
        Consume(TokenType.RightParen, "Expected ')'");
        return new CountExpressionAst(expr);
    }

    private ExpressionAst ParseConditionalOrAny()
    {
        Advance(); // Consume $?(
        var first = ParseLogicalOr();

        if (Match(TokenType.Nothing)) // ? token for Ternary
        {
            var thenExpr = ParseLogicalOr();
            Consume(TokenType.Pipe, "Expected '|' in ternary");
            var elseExpr = ParseLogicalOr();
            Consume(TokenType.RightParen, "Expected ')'");
            return new ConditionalExpressionAst(first, thenExpr, elseExpr);
        }

        var options = new List<ExpressionAst> { first };
        while (Match(TokenType.Pipe))
        {
            options.Add(ParseLogicalOr());
        }
        Consume(TokenType.RightParen, "Expected ')'");
        return new AnyExpressionAst(options.ToImmutableArray());
    }

    private bool IsAtEnd => _current >= tokens.Length;
}
