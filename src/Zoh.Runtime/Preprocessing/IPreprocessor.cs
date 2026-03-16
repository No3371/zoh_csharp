using System.Collections.Generic;
using System.Text.RegularExpressions;
using Zoh.Runtime.Diagnostics;
using Zoh.Runtime.Types;

namespace Zoh.Runtime.Preprocessing;

/// <summary>
/// Interface for a preprocessor that takes source text and transforms it.
/// </summary>
public interface IPreprocessor
{
    /// <summary>
    /// The priority of this preprocessor. Higher values run later.
    /// Recommended values:
    /// - Embeds: 100
    /// - Macros: 200
    /// - Sugar: 300
    /// </summary>
    int Priority { get; }

    /// <summary>
    /// Processes the source text.
    /// </summary>
    /// <param name="context">The context containing source text and helper methods.</param>
    /// <returns>The result of the preprocessing step.</returns>
    PreprocessorResult Process(PreprocessorContext context);
}

/// <summary>
/// Context passed to preprocessors.
/// </summary>
public class PreprocessorContext(string sourceText, string sourcePath)
{
    public string SourceText { get; } = sourceText;
    public string SourcePath { get; } = sourcePath;

    /// <summary>Runtime-scoped flags available for interpolation.</summary>
    public IReadOnlyDictionary<string, ZohValue> RuntimeFlags { get; init; } = new Dictionary<string, ZohValue>();

    /// <summary>Story metadata (key: value; pairs from the header), populated before embed processing.</summary>
    public Dictionary<string, string> Metadata { get; set; } = new();
}
