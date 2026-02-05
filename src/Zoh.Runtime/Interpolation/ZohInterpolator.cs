using System.Text;
using System.Text.RegularExpressions;
using Zoh.Runtime.Expressions;
using Zoh.Runtime.Lexing;
using Zoh.Runtime.Types;
using Zoh.Runtime.Variables;
using Zoh.Runtime.Parsing;

namespace Zoh.Runtime.Interpolation;

/// <summary>
/// Handles string interpolation logic, scanning for patterns like ${...} and replacing them with evaluated values.
/// </summary>
public class ZohInterpolator : IInterpolator
{
    private readonly ExpressionEvaluator _evaluator;

    public ZohInterpolator(VariableStore variables)
    {
        // New: Instantiate ExpressionEvaluator directly. No dependency on IInterpolator anymore.
        _evaluator = new ExpressionEvaluator(variables);
    }

    public string Interpolate(string template)
    {
        if (string.IsNullOrEmpty(template)) return template;

        var sb = new StringBuilder();
        var i = 0;
        while (i < template.Length)
        {
            // Check for escape sequence \{ or \}
            if (template[i] == '\\' && i + 1 < template.Length)
            {
                var next = template[i + 1];
                if (next == '{' || next == '}')
                {
                    sb.Append(next);
                    i += 2;
                    continue;
                }
            }

            var result = ScannerUtility.ScanPattern(template, i, '{', '}');
            if (result.Success)
            {
                var match = result.Match!;
                var consumed = match.FullText.Length;
                // Logic to evaluate the match
                try
                {
                    var val = EvaluateMatch(match);
                    sb.Append(val.ToString());
                }
                catch (Exception ex)
                {
                    throw new Exception($"Interpolation error in pattern '{match.FullText}': {ex.Message}", ex);
                }
                i += consumed;
            }
            else if (result.Error != null)
            {
                // Fatal syntax error
                throw new Exception($"invalid_syntax: {result.Error}");
            }
            else // NoMatch
            {
                sb.Append(template[i]);
                i++;
            }
        }
        return sb.ToString();
    }


    private ZohValue EvaluateMatch(MatchResult match)
    {
        // Transform to Expression Syntax
        string exprSource;

        // Handle Unroll {*var..."delim"}
        if (match.OpenToken == "${")
        {
            // Check for unroll pattern "..."
            var parts = match.Content.Split("...", 2, StringSplitOptions.None);
            if (parts.Length == 2)
            {
                var collectionExpr = parts[0].Trim();
                var delimExpr = parts[1].Trim(); // usually literal string "delim"
                                                 // Evaluate collection
                var colVal = EvaluateExprString(collectionExpr);
                var delimVal = EvaluateExprString(delimExpr);

                // Perform Unroll
                if (colVal is ZohList list)
                {
                    return new ZohStr(string.Join(delimVal.ToString(), list.Items.Select(x => x.ToString())));
                }
                // If not list? Unroll on single value? Just value.
                return colVal;
            }

            // ${ content } -> Evaluate(content)
            exprSource = match.Content;
        }
        else if (match.OpenToken == "$#{")
        {
            // $#{ content } -> Parse $#( content )
            exprSource = "$#(" + match.Content + ")";
        }
        else if (match.OpenToken == "$?{")
        {
            // $?{ content } -> Parse $?( content )
            exprSource = "$?(" + match.Content + ")";
        }
        else
        {
            throw new Exception("Unknown token " + match.OpenToken);
        }

        // Apply suffix if present
        if (!string.IsNullOrEmpty(match.Suffix))
        {
            // Safer to wrap: `$( exprSource )Suffix`.
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
        return _evaluator.Evaluate(ast);
    }
}

