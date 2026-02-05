using System.Text.RegularExpressions;
using Zoh.Runtime.Diagnostics;
using System.Text;
using System.Collections.Immutable;
using Zoh.Runtime.Lexing;

namespace Zoh.Runtime.Preprocessing;

public class EmbedPreprocessor(IFileReader fileReader) : IPreprocessor
{
    public int Priority => 100;

    private static readonly Regex EmbedRegex = new(@"^\s*#embed\s+""([^""]+)""\s*;", RegexOptions.Compiled);

    public PreprocessorResult Process(PreprocessorContext context)
    {
        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();
        var mappings = new List<SourceMapElement>();
        var embeddedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Add initial mapping
        mappings.Add(new SourceMapElement(0, context.SourcePath, 1));
        embeddedFiles.Add(context.SourcePath);

        var sb = new StringBuilder();
        int currentGenLine = 0;

        ProcessRecursive(context.SourceText, context.SourcePath,
            sb, mappings, diagnostics, embeddedFiles, ref currentGenLine);

        return new PreprocessorResult(
            sb.ToString(),
            new SourceMap(mappings),
            diagnostics.ToImmutable()
        );
    }

    private void ProcessRecursive(
        string sourceText,
        string currentFile,
        StringBuilder sb,
        List<SourceMapElement> mappings,
        ImmutableArray<Diagnostic>.Builder diagnostics,
        HashSet<string> embeddedFiles,
        ref int currentGenLine)
    {
        var lines = sourceText.Split('\n');

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];

            // Handle CRLF or LF - split keeps \r if present? 
            // string.Split('\n') keeps \r at end of strings.
            string lineContent = line.TrimEnd('\r');

            var match = EmbedRegex.Match(lineContent);
            if (match.Success)
            {
                var relativePath = match.Groups[1].Value;
                try
                {
                    var absPath = fileReader.ResolvePath(currentFile, relativePath);

                    if (embeddedFiles.Contains(absPath))
                    {
                        var pos = new TextPosition(i + 1, match.Groups[1].Index + 1, 0); // Approx
                        diagnostics.Add(new Diagnostic(
                            DiagnosticSeverity.Error,
                            "PRE001",
                            $"Circular dependency detected: {absPath}",
                            pos,
                            currentFile
                        ));
                        continue;
                    }

                    embeddedFiles.Add(absPath);
                    var embedContent = fileReader.ReadAllText(absPath);

                    // Add mapping for start of embedded file
                    mappings.Add(new SourceMapElement(currentGenLine, absPath, 1));

                    ProcessRecursive(embedContent, absPath, sb, mappings, diagnostics, embeddedFiles, ref currentGenLine);

                    // Restore mapping to current file after embed
                    mappings.Add(new SourceMapElement(currentGenLine, currentFile, i + 2));
                }
                catch (Exception ex)
                {
                    var pos = new TextPosition(i + 1, 0, 0);
                    diagnostics.Add(new Diagnostic(
                        DiagnosticSeverity.Error,
                        "PRE002",
                        $"Failed to embed '{relativePath}': {ex.Message}",
                        pos,
                        currentFile
                    ));
                }
            }
            else
            {
                sb.Append(lineContent);
                sb.Append('\n');
                currentGenLine++;
            }
        }
    }
}
