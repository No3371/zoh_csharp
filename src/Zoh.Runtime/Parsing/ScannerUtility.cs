using System;

namespace Zoh.Runtime.Parsing;

/// <summary>
/// Result of a pattern scan.
/// </summary>
public record MatchResult(string FullText, string OpenToken, string Content, string Suffix);

public class ScanResult
{
    public bool Success { get; }
    public MatchResult? Match { get; }
    public string? Error { get; }

    public static ScanResult Ok(MatchResult match) => new ScanResult(true, match, null);
    public static ScanResult Fail(string error) => new ScanResult(false, null, error);
    public static ScanResult NoMatch() => new ScanResult(false, null, null);

    private ScanResult(bool success, MatchResult? match, string? error)
    {
        Success = success;
        Match = match;
        Error = error;
    }
}

/// <summary>
/// Shared utility for scanning balanced patterns in strings (e.g. ${...}, $(...)).
/// </summary>
public static class ScannerUtility
{
    /// <summary>
    /// Scans for a pattern starting at <paramref name="start"/> in <paramref name="input"/>.
    /// </summary>
    public static ScanResult ScanPattern(string input, int start, char open, char close)
    {
        if (start >= input.Length) return ScanResult.NoMatch();
        if (input[start] != '$') return ScanResult.NoMatch();

        string openToken = null;
        int contentBodyStart = -1;

        // 1. Detect Prefix/OpenToken
        if (start + 1 >= input.Length) return ScanResult.NoMatch(); // End of string, just literal $

        char next = input[start + 1]; // skip $
        if (next == open)
        {
            openToken = "$" + open;
        }
        else if (next == '#' && start + 2 < input.Length && input[start + 2] == open)
        {
            openToken = "$#" + open;
        }
        else if (next == '?' && start + 2 < input.Length && input[start + 2] == open)
        {
            openToken = "$?" + open;
        }
        else
        {
            return ScanResult.NoMatch();
        }

        contentBodyStart = start + openToken.Length;

        // 2. Scan Balanced Content
        var current = contentBodyStart;
        var depth = 1;
        var inQuote = false;
        var quoteChar = '\0';

        while (current < input.Length && depth > 0)
        {
            var c = input[current];

            if (inQuote)
            {
                if (c == '\\')
                {
                    current++; // Skip escaped char
                }
                else if (c == quoteChar)
                {
                    inQuote = false;
                }
            }
            else
            {
                if (c == '"' || c == '\'')
                {
                    inQuote = true;
                    quoteChar = c;
                }
                else if (c == open)
                {
                    depth++;
                }
                else if (c == close)
                {
                    depth--;
                    if (depth == 0) break; // Found closing char
                }
            }
            current++;
        }

        if (depth != 0) return ScanResult.Fail($"Malformed interpolation syntax: Unclosed '{openToken}' starting at index {start}");

        var closeIndex = current; // index of closing char
        var content = input.Substring(contentBodyStart, closeIndex - contentBodyStart);

        // 3. Check Suffix [ ... ]
        var suffix = "";
        var consumed = closeIndex + 1 - start;
        var p = closeIndex + 1;

        if (p < input.Length && input[p] == '[')
        {
            var bStart = p;
            p++;
            var bDepth = 1;
            while (p < input.Length && bDepth > 0)
            {
                if (input[p] == '[') bDepth++;
                else if (input[p] == ']') bDepth--;
                p++;
            }
            if (bDepth != 0) return ScanResult.Fail($"Malformed interpolation syntax: Unclosed suffix brackets starting at index {bStart}");

            suffix = input.Substring(bStart, p - bStart);
            consumed = p - start;
        }

        return ScanResult.Ok(new MatchResult(input.Substring(start, consumed), openToken!, content, suffix));
    }
}
