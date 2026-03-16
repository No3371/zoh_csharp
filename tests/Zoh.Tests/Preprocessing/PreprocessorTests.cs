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
    [Fact]
    public void Macro_Expands_RelativeForward()
    {
        var processor = new MacroPreprocessor();
        var source = @"
|%REL%|
/log ""|%|"", ""|%+1|"";
|%REL%|

|%REL|A|B|C|%|
";
        var context = new PreprocessorContext(source, "/test.zoh");
        var result = processor.Process(context);

        if (!result.Success) throw new Exception("Macro RelFor failed: " + string.Join(", ", result.Diagnostics));
        // |%| = A (auto=0, then auto=1)
        // |%+1| = args[1+1] = args[2] = C
        Assert.Contains("/log \"A\", \"C\";", result.ProcessedText);
    }

    [Fact]
    public void Macro_Expands_RelativeBackward()
    {
        var processor = new MacroPreprocessor();
        var source = @"
|%REL%|
/log ""|%|"", ""|%|"", ""|%-1|"";
|%REL%|

|%REL|A|B|C|%|
";
        var context = new PreprocessorContext(source, "/test.zoh");
        var result = processor.Process(context);

        if (!result.Success) throw new Exception("Macro RelBack failed: " + string.Join(", ", result.Diagnostics));
        // |%| = A (auto=0->1), |%| = B (auto=1->2)
        // |%-1| (at usage 2) = args[2-1] = args[1] = B
        Assert.Contains("/log \"A\", \"B\", \"B\";", result.ProcessedText);
    }

    [Fact]
    public void Macro_PreservesIndentation()
    {
        var processor = new MacroPreprocessor();
        var source = @"
|%BLOCK%|
/if *x,
    /log ""yes"";
;
|%BLOCK%|

    |%BLOCK|%|
";
        var context = new PreprocessorContext(source, "/test.zoh");
        var result = processor.Process(context);

        if (!result.Success) throw new Exception("Macro Indent failed: " + string.Join(", ", result.Diagnostics));
        // Expanded lines should be indented by 4 spaces
        Assert.Contains("    /if *x,", result.ProcessedText);
        Assert.Contains("        /log \"yes\";", result.ProcessedText);
    }

    [Fact]
    public void Macro_SymmetricTrim_Basic()
    {
        var processor = new MacroPreprocessor();
        // "  A  " (2,2) -> "A"
        // " A " (1,1) -> "A"
        // " A  " (1,2) -> "A "
        // "  A " (2,1) -> " A"
        var source = @"
|%T%|
/v ""|%0|"";
|%T%|

|%T|  A  |%|
|%T| A |%|
|%T| A  |%|
|%T|  A |%|
";
        var context = new PreprocessorContext(source, "/test.zoh");
        var result = processor.Process(context);

        if (!result.Success) throw new Exception("Macro Trim failed: " + string.Join(", ", result.Diagnostics));
        Assert.Contains("/v \"A\";", result.ProcessedText);
        Assert.Contains("/v \"A \";", result.ProcessedText);
        Assert.Contains("/v \" A\";", result.ProcessedText);
    }

    // --- Embed interpolation and #embed? tests ---

    [Fact]
    public void Embed_InterpolatesFilename()
    {
        var reader = new MockFileReader();
        reader.AddFile("/main.zoh", "content");
        reader.AddFile("/main", "embedded content");

        var processor = new EmbedPreprocessor(reader);
        var context = new PreprocessorContext("#embed \"${filename}\";\n/done;", "/main.zoh");

        var result = processor.Process(context);

        if (!result.Success) throw new Exception("Embed failed: " + string.Join(", ", result.Diagnostics));
        Assert.Contains("embedded content", result.ProcessedText);
    }

    [Fact]
    public void Embed_InterpolatesRuntimeFlag()
    {
        var reader = new MockFileReader();
        reader.AddFile("/en.zoh", "/set *lang \"en\";");

        var processor = new EmbedPreprocessor(reader);
        var context = new PreprocessorContext("#embed \"${locale}.zoh\";", "/main.zoh")
        {
            RuntimeFlags = new Dictionary<string, string> { ["locale"] = "en" }
        };

        var result = processor.Process(context);

        if (!result.Success) throw new Exception("Embed failed: " + string.Join(", ", result.Diagnostics));
        Assert.Contains("/set *lang \"en\";", result.ProcessedText);
    }

    [Fact]
    public void Embed_InterpolatesMetadata()
    {
        var reader = new MockFileReader();
        reader.AddFile("/fr.zoh", "/set *lang \"fr\";");

        var processor = new EmbedPreprocessor(reader);
        var source = "My Story\nlocale: fr;\n===\n\n#embed \"${locale}.zoh\";";
        var context = new PreprocessorContext(source, "/main.zoh");

        var result = processor.Process(context);

        if (!result.Success) throw new Exception("Embed failed: " + string.Join(", ", result.Diagnostics));
        Assert.Contains("/set *lang \"fr\";", result.ProcessedText);
    }

    [Fact]
    public void Embed_ResolutionOrder_BuiltinBeforeFlag()
    {
        // "filename" is a built-in — runtime flag with same name should not override
        var reader = new MockFileReader();
        reader.AddFile("/main", "builtin wins");
        reader.AddFile("/flag-value", "flag wins");

        var processor = new EmbedPreprocessor(reader);
        var context = new PreprocessorContext("#embed \"${filename}\";", "/main.zoh")
        {
            RuntimeFlags = new Dictionary<string, string> { ["filename"] = "flag-value" }
        };

        var result = processor.Process(context);

        if (!result.Success) throw new Exception("Embed failed: " + string.Join(", ", result.Diagnostics));
        Assert.Contains("builtin wins", result.ProcessedText);
    }

    [Fact]
    public void Embed_UnknownVariable_ResolvesToEmpty()
    {
        var reader = new MockFileReader();
        reader.AddFile("/.zoh", "empty path content");

        var processor = new EmbedPreprocessor(reader);
        var context = new PreprocessorContext("#embed \"${unknown}.zoh\";", "/main.zoh");

        var result = processor.Process(context);

        if (!result.Success) throw new Exception("Embed failed: " + string.Join(", ", result.Diagnostics));
        Assert.Contains("empty path content", result.ProcessedText);
    }

    [Fact]
    public void EmbedOptional_SilentlySkips_WhenFileMissing()
    {
        var reader = new MockFileReader();
        // no files added — file not found

        var processor = new EmbedPreprocessor(reader);
        var context = new PreprocessorContext("#embed? \"missing.zoh\";\n/done;", "/main.zoh");

        var result = processor.Process(context);

        Assert.True(result.Success);
        Assert.DoesNotContain("missing.zoh", result.ProcessedText);
        Assert.Contains("/done;", result.ProcessedText);
    }

    [Fact]
    public void EmbedOptional_Embeds_WhenFileExists()
    {
        var reader = new MockFileReader();
        reader.AddFile("/optional.zoh", "/set *x 1;");

        var processor = new EmbedPreprocessor(reader);
        var context = new PreprocessorContext("#embed? \"optional.zoh\";\n/done;", "/main.zoh");

        var result = processor.Process(context);

        if (!result.Success) throw new Exception("Embed failed: " + string.Join(", ", result.Diagnostics));
        Assert.Contains("/set *x 1;", result.ProcessedText);
        Assert.Contains("/done;", result.ProcessedText);
    }

    [Fact]
    public void EmbedOptional_StillFatal_OnCircularDependency()
    {
        var reader = new MockFileReader();
        reader.AddFile("/a.zoh", "#embed? \"b.zoh\";");
        reader.AddFile("/b.zoh", "#embed? \"a.zoh\";");

        var processor = new EmbedPreprocessor(reader);
        var context = new PreprocessorContext("#embed? \"a.zoh\";", "/main.zoh");

        var result = processor.Process(context);

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, d => d.Code == "PRE001");
    }

    [Fact]
    public void Embed_Static_BehaviorUnchanged()
    {
        var reader = new MockFileReader();
        reader.AddFile("/lib.zoh", "/set *x 42;");

        var processor = new EmbedPreprocessor(reader);
        var context = new PreprocessorContext("#embed \"lib.zoh\";\n/set *y 2;", "/main.zoh");

        var result = processor.Process(context);

        if (!result.Success) throw new Exception("Embed failed: " + string.Join(", ", result.Diagnostics));
        var lines = result.ProcessedText.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal("/set *x 42;", lines[0].Trim());
        Assert.Equal("/set *y 2;", lines[1].Trim());
    }

    [Fact]
    public void Macro_Escaping_Percent()
    {
        var processor = new MacroPreprocessor();
        var source = @"
|%E%|
/v ""|%0|"";
|%E%|

|%E| \% |%|
|%E| 100\% |%|
";
        // Note: In C# string literal @"...", backslash is just backslash.
        // So " \% " passes " \% " to the preprocessor.
        // The preprocessor should unescape it to "%".

        var context = new PreprocessorContext(source, "/test.zoh");
        var result = processor.Process(context);

        if (!result.Success) throw new Exception("Macro Escape failed: " + string.Join(", ", result.Diagnostics));

        // " \% " -> trimmed " \% " -> unescaped "%" (Wait, if trimmed first, it handles spaces)
        // With symmetric trim: " \% " (1,1) -> "\%" -> unescape -> "%"
        Assert.DoesNotContain("\\%", result.ProcessedText);
        Assert.Contains("/v \"%\";", result.ProcessedText);
        Assert.Contains("/v \"100%\";", result.ProcessedText);
    }
}
