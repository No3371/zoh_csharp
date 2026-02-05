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
            // Simple mock resolution
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

        Assert.True(result.Success);
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
    public void SourceMap_MapsEmbedLinesCorrectly()
    {
        var reader = new MockFileReader();
        reader.AddFile("/lib.zoh", "lib1\nlib2");

        var processor = new EmbedPreprocessor(reader);
        var source = @"main1
#embed ""lib.zoh"";
main2";

        var context = new PreprocessorContext(source, "/main.zoh");
        var result = processor.Process(context);

        // Result:
        // main1 (line 0) -> main.zoh line 0 (1-based: 1)
        // lib1  (line 1) -> /lib.zoh line 0 (1-based: 1)
        // lib2  (line 2) -> /lib.zoh line 1 (1-based: 2)
        // main2 (line 3) -> main.zoh line 2? (original line 3)

        var map = result.SourceMap!;

        // Check line 0 (main1)
        var (file0, line0) = map.Map(0);
        Assert.Equal("/main.zoh", file0);
        Assert.Equal(1, line0); // 1-based index in TextPosition/SourceMap usually?
                                // Wait, SourceMap implementation needs to be checked for 0 vs 1 based.
                                // EmbedPreprocessor: new SourceMapElement(0, context.SourcePath, 0));
                                // Element(GeneratedLine, File, OriginalLine).
                                // If OriginalLine is 0-based index or 1-based number?
                                // EmbedPreprocessor code uses loop index `i` (0-based) but creates invalid lines?
                                // `i + 1` was used in EmbedPreprocessor: `mappings.Add(..., i + 1));`
                                // So original lines are 1-based.

        // Check line 1 (lib1)
        var (file1, line1) = map.Map(1);
        Assert.Equal("/lib.zoh", file1);
        Assert.Equal(1, line1); // Start of lib

        // Check line 3 (main2)
        var (file3, line3) = map.Map(3);
        Assert.Equal("/main.zoh", file3);
        Assert.Equal(3, line3); // line 3 in original source (index 2 + 1)
    }
    [Fact]
    public void Macro_DefinesAndExpands()
    {
        var processor = new MacroPreprocessor();
        var source = @"
#macro GREET name;
/say ""Hello, |#name#|!"";
#macro;

#expand GREET name:""World"";
";
        var context = new PreprocessorContext(source, "/test.zoh");
        var result = processor.Process(context);

        Assert.True(result.Success, string.Join("\n", result.Diagnostics));
        Assert.DoesNotContain("#macro", result.ProcessedText);
        Assert.Contains("/say \"Hello, \"World\"!\";", result.ProcessedText);
    }

    [Fact]
    public void Macro_ExpandsWithDefaults()
    {
        var processor = new MacroPreprocessor();
        var source = @"
#macro LOG msg, level;
/log level:|#level|""INFO""#| message:|#msg#|;
#macro;

#expand LOG msg:""Start"";
#expand LOG msg:""Error"", level:""ERROR"";
";
        var context = new PreprocessorContext(source, "/test.zoh");
        var result = processor.Process(context);

        Assert.True(result.Success, string.Join("\n", result.Diagnostics));
        var lines = result.ProcessedText.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        // First expansion use default level
        // Note: Lexer keeps quotes for string values. So msg:""Start"" -> val is ""Start""
        // Placeholder |#level|"INFO"#| gets replaced by default "INFO" (including quotes if written in macro def)
        // Wait, current Regex |#level|"INFO"#| extracts default as "INFO" (with quotes).
        // So result: /log level:"INFO" message:"Start"; 

        Assert.Contains("/log level:\"INFO\" message:\"Start\";", result.ProcessedText);
        Assert.Contains("/log level:\"ERROR\" message:\"Error\";", result.ProcessedText);
    }

    [Fact]
    public void Macro_ErrorOnMissingParam()
    {
        var processor = new MacroPreprocessor();
        var source = @"
#macro REQUIRED p;
|#p#|
#macro;

#expand REQUIRED;
";
        var context = new PreprocessorContext(source, "/test.zoh");
        var result = processor.Process(context);

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, d => d.Code == "PRE006"); // Missing parameter
    }
}
