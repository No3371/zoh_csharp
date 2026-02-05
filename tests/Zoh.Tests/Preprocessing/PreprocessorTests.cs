using System.Collections.Immutable;
using Zoh.Runtime.Diagnostics;
using Zoh.Runtime.Preprocessing;

namespace Zoh.Tests.Preprocessing;

public class PreprocessorTests
{
    private class MockFileReader : IFileReader
    {
        private readonly Dictionary<string, string> _files = new();

        public void AddFile(string path, string content) => _files[path] = content;

        public string ReadAllText(string path)
        {
            if (_files.TryGetValue(path, out var content)) return content;
            throw new FileNotFoundException($"File not found: {path}");
        }

        public string ResolvePath(string basePath, string relativePath)
        {
            return relativePath.StartsWith("/") ? relativePath : $"/{relativePath}";
        }
    }

    [Fact]
    public void Embed_ReplacesDirectiveWithContent()
    {
        var reader = new MockFileReader();
        reader.AddFile("/lib.zoh", "/set *x 1;");

        var processor = new EmbedPreprocessor(reader);
        var context = new PreprocessorContext(
            "#embed \"lib.zoh\";\n/set *y 2;",
            "/main.zoh"
        );

        var result = processor.Process(context);

        if (!result.Success) throw new Exception("Embed failed: " + string.Join(", ", result.Diagnostics));
        var lines = result.ProcessedText.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal("/set *x 1;", lines[0].Trim());
        Assert.Equal("/set *y 2;", lines[1].Trim());
    }

    [Fact]
    public void Embed_DetectsCircularDependency()
    {
        var reader = new MockFileReader();
        reader.AddFile("/a.zoh", "#embed \"b.zoh\";");
        reader.AddFile("/b.zoh", "#embed \"a.zoh\";");

        var processor = new EmbedPreprocessor(reader);
        var context = new PreprocessorContext("#embed \"a.zoh\";", "/main.zoh");

        var result = processor.Process(context);

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, d => d.Code == "PRE001");
    }

    [Fact]
    public void Macro_DefinesAndExpands_NoArgs()
    {
        var processor = new MacroPreprocessor();
        var source = @"
|%GREET%|
/say ""Hello!"";
|%GREET%|

|%GREET|%|
// |%GREET|%| is the correct no-arg syntax.
";
        var context = new PreprocessorContext(source, "/test.zoh");
        var result = processor.Process(context);

        if (!result.Success) throw new Exception("Macro NoArgs failed: " + string.Join(", ", result.Diagnostics));
        Assert.DoesNotContain("|%GREET|%|", result.ProcessedText);
        Assert.Contains("/say \"Hello!\";", result.ProcessedText);
    }

    [Fact]
    public void Macro_Expands_PositionalArgs()
    {
        var processor = new MacroPreprocessor();
        var source = @"
|%SAY%|
/say ""|%0|"", ""|%1|"";
|%SAY%|

|%SAY|Hello|World|%|
";
        var context = new PreprocessorContext(source, "/test.zoh");
        var result = processor.Process(context);

        if (!result.Success) throw new Exception("Macro Positional failed: " + string.Join(", ", result.Diagnostics));
        Assert.Contains("/say \"Hello\", \"World\";", result.ProcessedText);
    }

    [Fact]
    public void Macro_Expands_AutoIncArgs()
    {
        var processor = new MacroPreprocessor();
        var source = @"
|%LIST%|
/item |%|;
/item |%|;
/item |%|;
|%LIST%|

|%LIST|A|B|C|%|
";
        var context = new PreprocessorContext(source, "/test.zoh");
        var result = processor.Process(context);

        if (!result.Success) throw new Exception("Macro AutoInc failed: " + string.Join(", ", result.Diagnostics));
        Assert.Contains("/item A;", result.ProcessedText);
        Assert.Contains("/item B;", result.ProcessedText);
        Assert.Contains("/item C;", result.ProcessedText);
    }

    [Fact]
    public void Macro_HandleMissingArg_AsEmptyString()
    {
        var processor = new MacroPreprocessor();
        var source = "|%CHECK%|\n/val |%0|;\n|%CHECK%|\n\n|%CHECK|%|";

        // 0 args provided
        var context = new PreprocessorContext(source, "/test.zoh");
        var result = processor.Process(context);

        if (!result.Success) throw new Exception("Macro MissingArg failed: " + string.Join(", ", result.Diagnostics));
        try
        {
            Assert.Contains("/val ;", result.ProcessedText);
        }
        catch (Exception)
        {
            throw new Exception($"MissingArg Assertion Failed. Actual: '{result.ProcessedText}'");
        }
    }

    [Fact]
    public void Macro_HandlesEscapedPipes()
    {
        var processor = new MacroPreprocessor();
        var source = @"
|%PIPE%|
/log ""|%0|"";
|%PIPE%|

|%PIPE|A \| B|%|
";
        var context = new PreprocessorContext(source, "/test.zoh");
        var result = processor.Process(context);

        if (!result.Success) throw new Exception("Macro Escaped failed: " + string.Join(", ", result.Diagnostics));
        Assert.Contains("/log \"A | B\";", result.ProcessedText);
    }

    [Fact]
    public void Macro_HandlesMultilineArgs()
    {
        var processor = new MacroPreprocessor();
        var source = @"
|%LOG%|
/log ""|%0|"";
|%LOG%|

|%LOG|Line1
Line2|%|
";
        var context = new PreprocessorContext(source, "/test.zoh");
        var result = processor.Process(context);

        if (!result.Success) throw new Exception("Macro Multiline failed: " + string.Join(", ", result.Diagnostics));
        // Expect exact newline preservation
        Assert.Contains("Line1\nLine2", result.ProcessedText.Replace("\r\n", "\n"));
    }
}
