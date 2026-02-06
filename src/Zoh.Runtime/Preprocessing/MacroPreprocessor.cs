using System.Text.RegularExpressions;
using System.Text;
using System.Collections.Generic;
using System.Collections.Immutable;
using Zoh.Runtime.Diagnostics;
using Zoh.Runtime.Lexing;

namespace Zoh.Runtime.Preprocessing;

public class MacroPreprocessor : IPreprocessor
{
    private record MacroDef(string Name, string Body, string SourceFile, int SourceLine);

    public int Priority => 200; // After Embed(100), before Sugar(300)

    public PreprocessorResult Process(PreprocessorContext context)
    {
        var (sourceWithoutMacros, macros, diagnostics) = CollectMacros(context);

        // If collection failed fatally, return early (though diagnostics checks are enough)
        // Expand using the collected macros
        var expandedText = ExpandMacros(sourceWithoutMacros, macros, diagnostics);

        return new PreprocessorResult(expandedText, null, diagnostics.ToImmutable());
    }

    private (string, Dictionary<string, MacroDef>, ImmutableArray<Diagnostic>.Builder) CollectMacros(PreprocessorContext context)
    {
        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();
        var macros = new Dictionary<string, MacroDef>();
        var sb = new StringBuilder();
        var lines = context.SourceText.Split('\n');

        // Definition Pattern: ^\s*\|%(\w+)%\|\s*$
        var defStartRegex = new Regex(@"^\s*\|%(\w+)%\|\s*$", RegexOptions.Compiled);

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var match = defStartRegex.Match(line);

            if (match.Success)
            {
                var name = match.Groups[1].Value;
                var startLine = i + 1;
                // Collect body
                var bodySb = new StringBuilder();
                i++;
                bool terminated = false;

                var endTag = $"|%{name}%|";

                while (i < lines.Length)
                {
                    if (lines[i].Trim() == endTag)
                    {
                        terminated = true;
                        break;
                    }
                    bodySb.Append(lines[i]).Append('\n');
                    i++;
                }

                if (!terminated)
                {
                    // Diagnostic(Severity, Code, Message, Position, FilePath)
                    diagnostics.Add(new Diagnostic(DiagnosticSeverity.Error, "PRE002", "Unterminated macro definition", new TextPosition(startLine, 1, 0), context.SourcePath));
                }
                else
                {
                    if (macros.ContainsKey(name))
                    {
                        diagnostics.Add(new Diagnostic(DiagnosticSeverity.Warning, "PRE003", $"Duplicate macro definition '{name}'", new TextPosition(startLine, 1, 0), context.SourcePath));
                    }
                    macros[name] = new MacroDef(name, bodySb.ToString(), context.SourcePath, startLine);
                }
            }
            else
            {
                sb.Append(line).Append('\n');
            }
        }

        return (sb.ToString(), macros, diagnostics);
    }

    private string ExpandMacros(string source, Dictionary<string, MacroDef> macros, ImmutableArray<Diagnostic>.Builder diagnostics)
    {
        // Expansion Pattern:
        // Args:   |%NAME|arg1|arg2...|%|
        // No-Arg: |%NAME|%| (effectively empty args list)

        var sb = new StringBuilder();
        int index = 0;

        while (index < source.Length)
        {
            int openIdx = source.IndexOf("|%", index);
            if (openIdx == -1)
            {
                sb.Append(source.Substring(index));
                break;
            }

            sb.Append(source.Substring(index, openIdx - index));
            index = openIdx; // At |%

            // Parse Name
            int nameStart = index + 2;

            // Look ahead for name
            var remainder = source.Substring(nameStart);
            var nameMatch = Regex.Match(remainder, @"^(\w+)");

            if (!nameMatch.Success)
            {
                // Not a macro start, just literal |%
                sb.Append("|%");
                index += 2;
                continue;
            }

            string name = nameMatch.Value;
            int nameEnd = nameStart + name.Length;

            // Expected char at nameEnd must be '|' (start of args or empty-arg terminator start)

            if (nameEnd < source.Length && source[nameEnd] == '|')
            {
                // Scan for closing |%| taking escapes into account
                int argsStart = nameEnd + 1;
                int current = argsStart;
                bool foundEnd = false;

                while (current < source.Length - 1)
                {
                    // Check for terminator %|
                    // Structure is ... | %|
                    if (source[current] == '%' && source[current + 1] == '|')
                    {
                        foundEnd = true;
                        break;
                    }

                    if (source[current] == '\\' && current + 1 < source.Length)
                    {
                        current += 2; // Skip escaped char
                    }
                    else
                    {
                        current++;
                    }
                }

                if (foundEnd)
                {
                    string argsContent = source.Substring(argsStart, current - argsStart);

                    if (macros.TryGetValue(name, out var macro))
                    {
                        var args = ParseArgs(argsContent);

                        // Capture indentation
                        int lineStart = source.LastIndexOf('\n', index); // Index is currently at |%... openIdx
                                                                         // openIdx is passed as index to this logic, but wait. 
                                                                         // In the loop: index = openIdx; 
                                                                         // So 'index' is the start of |%.

                        // lineStart is the index of newline before |%.
                        // If -1, start of string.
                        int scanStart = lineStart == -1 ? 0 : lineStart + 1;
                        var indent = new StringBuilder();
                        for (int k = scanStart; k < index; k++)
                        {
                            if (char.IsWhiteSpace(source[k]))
                                indent.Append(source[k]);
                            else
                                indent.Clear(); // Not reliable indentation if non-whitespace exists? 
                                                // Actually, spec says "continuous white spaces from line start".
                                                // If there are non-whitespaces, strict indentation might preserve them or restart?
                                                // "found before the |% token" implies immediately preceding?
                                                // "Indentation (continuous white spaces from line start)"
                                                // So if we have "  abc  |%MACRO|", indentation is "  abc  "?
                                                // Or does it mean line must ONLY contain whitespace?
                                                // Spec: "The macro expansion preserves the indentation (continuous white spaces from line start) of the usage line."
                                                // This implies we take all whitespace from line start.
                        }

                        // Re-scanning correctly:
                        string indentStr = "";
                        int lastNewline = source.LastIndexOf('\n', index - 1); // index is |%
                        int checkStart = lastNewline == -1 ? 0 : lastNewline + 1;
                        bool referenceIsLineStart = true;

                        var indentSb = new StringBuilder();
                        for (int k = checkStart; k < index; k++)
                        {
                            if (!char.IsWhiteSpace(source[k]))
                            {
                                referenceIsLineStart = false;
                                break;
                            }
                            indentSb.Append(source[k]);
                        }

                        if (referenceIsLineStart)
                        {
                            indentStr = indentSb.ToString();
                        }

                        var expanded = ExpandBody(macro.Body, args);
                        if (!string.IsNullOrEmpty(indentStr) && expanded.Contains('\n'))
                        {
                            expanded = expanded.Replace("\n", "\n" + indentStr);
                        }
                        sb.Append(expanded);
                    }
                    else
                    {
                        diagnostics.Add(new Diagnostic(DiagnosticSeverity.Error, "PRE004", $"Unknown macro '{name}'", new TextPosition(1, 1, index), "unknown"));
                        // Keep original text on error
                        sb.Append(source.Substring(index, (current + 2) - index));
                    }
                    index = current + 2; // Skip %|
                }
                else
                {
                    // Unterminated expansion (failed to find %|)
                    // Note: If |%NAME|%| -> nameEnd is |, argsStart is %.
                    // Loop starts at %. Check if % followed by |. Yes.
                    // foundEnd = true.
                    // argsContent = substring(%, %-%) = ""?
                    // argsStart is index of %. current is index of %.
                    // length is 0.
                    // ParseArgs("") returns [""]. Correct.

                    diagnostics.Add(new Diagnostic(DiagnosticSeverity.Error, "PRE005", $"Unterminated expansion for '{name}'", new TextPosition(1, 1, index), "unknown"));
                    sb.Append("|%").Append(name).Append("|");
                    index = nameEnd + 1;
                }
            }
            else
            {
                // Just |%NAME followed by something else (e.g. %| or nothing)
                // Treat as literal
                sb.Append("|%").Append(name);
                index = nameEnd;
            }
        }

        return sb.ToString();
    }

    private List<string> ParseArgs(string argsContent)
    {
        if (string.IsNullOrEmpty(argsContent)) return new List<string> { "" };

        var args = new List<string>();
        // Split by | but respect \| escape
        // We can scan manually

        var currentArg = new StringBuilder();
        for (int i = 0; i < argsContent.Length; i++)
        {
            if (argsContent[i] == '\\' && i + 1 < argsContent.Length && argsContent[i + 1] == '|')
            {
                currentArg.Append('|'); // Unescape
                i++;
            }
            else if (argsContent[i] == '\\' && i + 1 < argsContent.Length && argsContent[i + 1] == '%')
            {
                currentArg.Append('%'); // Unescape
                i++;
            }
            else if (argsContent[i] == '|')
            {
                args.Add(SymmetricTrim(currentArg.ToString()));
                currentArg.Clear();
            }
            else
            {
                currentArg.Append(argsContent[i]);
            }
        }
        args.Add(SymmetricTrim(currentArg.ToString())); // Add last arg
        return args;
    }

    private string SymmetricTrim(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;

        int leading = 0;
        while (leading < input.Length && char.IsWhiteSpace(input[leading])) leading++;

        if (leading == input.Length) return ""; // All whitespace

        int trailing = 0;
        while (trailing < input.Length && char.IsWhiteSpace(input[input.Length - 1 - trailing])) trailing++;

        int toRemove = Math.Min(leading, trailing);

        return input.Substring(toRemove, input.Length - (toRemove * 2));
    }

    private string ExpandBody(string body, List<string> args)
    {
        // Placeholders:
        // |%N| -> args[N]
        // |%| -> args[auto++]
        // |%+N| -> args[curr+N]
        // |%-N| -> args[curr-N]
        // \| -> |

        var result = new StringBuilder();
        int autoIndex = 0;

        int i = 0;
        while (i < body.Length)
        {
            // Placeholder start: |%
            int pStart = body.IndexOf("|%", i);
            if (pStart == -1)
            {
                result.Append(body.Substring(i));
                break;
            }

            // Append text before placeholder
            result.Append(body.Substring(i, pStart - i));

            // Scan for closing |
            int pEnd = body.IndexOf('|', pStart + 2);
            if (pEnd == -1)
            {
                // Not a placeholder
                result.Append("|%");
                i = pStart + 2;
                continue;
            }

            string content = body.Substring(pStart + 2, pEnd - (pStart + 2));
            int targetIdx = -1;

            if (string.IsNullOrEmpty(content))
            {
                // |%| -> auto increment
                targetIdx = autoIndex;
                autoIndex++;
            }
            else if (int.TryParse(content, out int idx))
            {
                if (content.StartsWith("+") || content.StartsWith("-"))
                {
                    targetIdx = autoIndex + idx;
                }
                else
                {
                    targetIdx = idx;
                }
            }
            else
            {
                // Unknown placeholder syntax, treat as literal? or ignore?
                // Append original
                result.Append(body.Substring(pStart, pEnd - pStart + 1));
                i = pEnd + 1;
                continue;
            }

            // Replacement logic
            string replacement = (targetIdx >= 0 && targetIdx < args.Count) ? args[targetIdx] : "";
            result.Append(replacement);

            i = pEnd + 1;
        }

        return result.ToString().Replace(@"\|", "|");
    }
}
