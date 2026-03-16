using System.IO;
using System.Text.RegularExpressions;
using Zoh.Runtime.Diagnostics;
using System.Text;
using System.Collections.Immutable;
using Zoh.Runtime.Lexing;

namespace Zoh.Runtime.Preprocessing;

public class EmbedPreprocessor(IFileReader fileReader) : IPreprocessor
{
    public int Priority => 100;

    // Group 1: optional '?', Group 2: path
    private static readonly Regex EmbedRegex = new(@"^\s*#embed(\??)\s+""([^""]+)""\s*;", RegexOptions.Compiled);
    private static readonly Regex InterpolationRegex = new(@"\$\{(\w+)\}", RegexOptions.Compiled);

    public PreprocessorResult Process(PreprocessorContext context)
    {
        ExtractMetadata(context);

        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();
        var mappings = new List<SourceMapElement>();
        var embeddedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        mappings.Add(new SourceMapElement(0, context.SourcePath, 1));
        embeddedFiles.Add(context.SourcePath);

        var sb = new StringBuilder();
        int currentGenLine = 0;

        ProcessRecursive(context.SourceText, context.SourcePath, context,
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
        PreprocessorContext context,
        StringBuilder sb,
        List<SourceMapElement> mappings,
        ImmutableArray<Diagnostic>.Builder diagnostics,
        HashSet<string> embeddedFiles,
        ref int currentGenLine)
    {
        var lines = sourceText.Split('\n');

        for (int i = 0; i < lines.Length; i++)
        {
            string lineContent = lines[i].TrimEnd('\r');

            var match = EmbedRegex.Match(lineContent);
            if (match.Success)
            {
                var isOptional = match.Groups[1].Value == "?";
                var rawPath = match.Groups[2].Value;
                var relativePath = InterpolatePath(rawPath, context);

                try
                {
                    var absPath = fileReader.ResolvePath(currentFile, relativePath);

                    if (embeddedFiles.Contains(absPath))
                    {
                        var pos = new TextPosition(i + 1, match.Groups[2].Index + 1, 0);
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

                    mappings.Add(new SourceMapElement(currentGenLine, absPath, 1));
                    ProcessRecursive(embedContent, absPath, context, sb, mappings, diagnostics, embeddedFiles, ref currentGenLine);
                    mappings.Add(new SourceMapElement(currentGenLine, currentFile, i + 2));
                }
                catch (FileNotFoundException) when (isOptional)
                {
                    // #embed? — silently skip missing file
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

    private static void ExtractMetadata(PreprocessorContext context)
    {
        if (context.Metadata.Count > 0) return;

        var separatorIndex = context.SourceText.IndexOf("\n===");
        if (separatorIndex < 0) return;

        var header = context.SourceText[..separatorIndex];
        var lines = header.Split('\n');

        for (int i = 1; i < lines.Length; i++)
        {
            var line = lines[i].TrimEnd('\r').Trim();
            var colonIdx = line.IndexOf(':');
            if (colonIdx > 0 && line.EndsWith(';'))
            {
                var key = line[..colonIdx].Trim();
                var value = line[(colonIdx + 1)..^1].Trim();
                if (value.Length >= 2 &&
                    ((value[0] == '"' && value[^1] == '"') ||
                     (value[0] == '\'' && value[^1] == '\'')))
                    value = value[1..^1];
                context.Metadata[key] = value;
            }
        }
    }

    private static string InterpolatePath(string path, PreprocessorContext context)
    {
        return InterpolationRegex.Replace(path, match =>
        {
            var name = match.Groups[1].Value;

            // 1. Built-in vars
            if (name == "filename" && !string.IsNullOrEmpty(context.SourcePath))
                return Path.GetFileNameWithoutExtension(context.SourcePath);

            // 2. Runtime flags
            if (context.RuntimeFlags.TryGetValue(name, out var flagVal))
                return flagVal;

            // 3. Story metadata
            if (context.Metadata.TryGetValue(name, out var metaVal))
                return metaVal;

            // 4. Unknown → empty string
            return "";
        });
    }
}
