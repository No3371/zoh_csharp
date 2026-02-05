using System.Collections.Immutable;
using Zoh.Runtime.Lexing;

namespace Zoh.Runtime.Preprocessing;

/// <summary>
/// Maps positions in processed text back to the original source location.
/// Crucial for error reporting when preprocessors (like #embed or #macro) change line numbers.
/// </summary>
public record SourceMapElement(int GeneratedLine, string OriginalFile, int OriginalLine);

public class SourceMap
{
    // Maps generated line index (0-based) to source information
    private readonly ImmutableArray<SourceMapElement> _mappings;

    public SourceMap(IEnumerable<SourceMapElement> mappings)
    {
        _mappings = mappings.OrderBy(m => m.GeneratedLine).ToImmutableArray();
    }

    public (string File, int Line) Map(int generatedLine)
    {
        // Binary search to find the mapping that covers this line
        // Mappings are sparse: they define where a new file/block starts

        int min = 0;
        int max = _mappings.Length - 1;
        SourceMapElement? bestMatch = null;

        while (min <= max)
        {
            int mid = (min + max) / 2;
            var current = _mappings[mid];

            if (current.GeneratedLine <= generatedLine)
            {
                bestMatch = current;
                min = mid + 1;
            }
            else
            {
                max = mid - 1;
            }
        }

        if (bestMatch != null)
        {
            // Calculate offset from the start of this block
            int offset = generatedLine - bestMatch.GeneratedLine;
            return (bestMatch.OriginalFile, bestMatch.OriginalLine + offset);
        }

        return ("", generatedLine); // Fallback: no mapping found
    }
}
