# Embed Variable Interpolation & Optional #embed? — C# Implementation

> **Status:** Complete
> **Created:** 2026-03-16
> **Completed:** 2026-03-16
> **Walkthrough:** `20260316-embed-variable-interpolation-impl-walkthrough.md`
> **Author:** Claude
> **Source:** Spec commit `5c97b07`; impl spec `03_preprocessor.md`
> **Related Projex:** `20260316-spec-catchup-followup.md`, `20260316-runtime-scoped-flags-impl-plan.md`
> **Worktree:** Yes

---

## Summary

Update `EmbedPreprocessor` to support `${name}` path interpolation and the `#embed?` optional embed variant. Interpolation resolves variables from built-in vars (filename), runtime flags, and story metadata in that order.

**Scope:** EmbedPreprocessor, PreprocessorContext extension, metadata extraction
**Estimated Changes:** 3 files modified, 1 file created

---

## Objective

### Problem / Gap / Need

The spec (`spec/1_concepts.md`, `impl/03_preprocessor.md`) defines:
1. `#embed "${filename}.${locale}.zoh";` — path interpolation from built-ins, runtime flags, metadata
2. `#embed? "path";` — silently skip if file not found (other errors remain fatal)

The C# `EmbedPreprocessor` only handles static `#embed "path";`. No interpolation, no optional variant.

### Success Criteria

- [ ] `#embed "${filename}.zoh";` resolves `${filename}` to current file's base name
- [ ] `#embed "${locale}.zoh";` resolves `${locale}` from runtime flags
- [ ] `#embed "${meta_key}.zoh";` resolves from story metadata
- [ ] Resolution order: built-in → runtime flag → metadata → empty string
- [ ] `#embed?` skips silently when file not found
- [ ] `#embed?` still fatal on circular embed
- [ ] Static `#embed` behavior unchanged
- [ ] `dotnet test` passes

### Out of Scope

- `#strsub` preprocessor directive
- Standard flag values/defaults
- Macro interpolation

---

## Context

### Current State

`EmbedPreprocessor` (`src/Zoh.Runtime/Preprocessing/EmbedPreprocessor.cs`) uses a regex to match `#embed "path";`, resolves the file via `IFileReader`, replaces the directive with file contents. No interpolation. No `?` variant.

`PreprocessorContext` holds `SourceText` and `SourcePath`. No metadata or flag access.

Story metadata is extracted by the Parser *after* preprocessing — creating a timing problem. Solution: extract metadata early with a lightweight regex pass before embed processing.

### Key Files

| File | Role | Change Summary |
|------|------|----------------|
| `src/Zoh.Runtime/Preprocessing/EmbedPreprocessor.cs` | Embed processor | Add interpolation, `#embed?` handling |
| `src/Zoh.Runtime/Preprocessing/PreprocessorContext.cs` | Preprocessor input | Add `Metadata` property |
| `src/Zoh.Runtime/Preprocessing/MetadataExtractor.cs` | New: early metadata extraction | Extract metadata before embed runs |
| `src/Zoh.Runtime/Execution/ZohRuntime.cs` | Pipeline wiring | Register MetadataExtractor, populate context |

### Dependencies

- **Requires:** `20260316-runtime-scoped-flags-impl-plan.md` — runtime flags must exist in PreprocessorContext
- **Blocks:** Nothing directly

### Constraints

- Metadata extraction must happen before embed processing (priority ordering)
- `${name}` syntax must not conflict with ZOH string interpolation (`${*var}` uses `*` prefix)
- Unknown `${name}` resolves to empty string (not an error)

### Assumptions

- `PreprocessorContext` already has `RuntimeFlags` from the flags plan
- Preprocessors run in priority order (existing behavior)
- Story header format (`Story Name\nmeta: value;\n===`) is stable enough for regex extraction
- `IFileReader` interface is unchanged

### Impact Analysis

- **Direct:** EmbedPreprocessor, PreprocessorContext, ZohRuntime.LoadStory
- **Adjacent:** MacroPreprocessor (unaffected — runs after embed)
- **Downstream:** Localization workflows using `#embed "${locale}/strings.zoh";`

---

## Implementation

### Overview

Four steps: (1) create MetadataExtractor preprocessor at priority 50, (2) extend PreprocessorContext with Metadata, (3) update EmbedPreprocessor regex and add interpolation + optional handling, (4) register MetadataExtractor in pipeline.

### Step 1: Create MetadataExtractor preprocessor

**Objective:** Extract story metadata from raw source before embed processing.
**Confidence:** Medium — regex extraction of metadata is approximate but sufficient for interpolation.
**Depends on:** None

**Files:**
- `src/Zoh.Runtime/Preprocessing/MetadataExtractor.cs` (new)

**Changes:**

```csharp
public class MetadataExtractor : IPreprocessor
{
    public int Priority => 50; // Before EmbedPreprocessor (100)

    public PreprocessorResult Process(PreprocessorContext context)
    {
        var metadata = new Dictionary<string, string>();

        // Find header section (before ===)
        var separatorIndex = context.SourceText.IndexOf("\n===");
        if (separatorIndex < 0)
        {
            context.Metadata = metadata;
            return new PreprocessorResult(context.SourceText, null,
                ImmutableArray<Diagnostic>.Empty);
        }

        var header = context.SourceText[..separatorIndex];
        var lines = header.Split('\n');

        // Skip first line (story name), parse key: value; lines
        for (int i = 1; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            var colonIdx = line.IndexOf(':');
            if (colonIdx > 0 && line.EndsWith(';'))
            {
                var key = line[..colonIdx].Trim();
                var value = line[(colonIdx + 1)..^1].Trim();
                // Strip quotes if present
                if (value.Length >= 2 &&
                    ((value[0] == '"' && value[^1] == '"') ||
                     (value[0] == '\'' && value[^1] == '\'')))
                    value = value[1..^1];
                metadata[key] = value;
            }
        }

        context.Metadata = metadata;
        return new PreprocessorResult(context.SourceText, null,
            ImmutableArray<Diagnostic>.Empty);
    }
}
```

**Rationale:** Simple regex-level extraction. Metadata is parsed again by the full Parser later — this is a lightweight early pass for interpolation only.

**Verification:** Feed source with metadata header → `context.Metadata` populated correctly.

**If this fails:** If metadata syntax is more complex than `key: value;`, refine the regex. The Parser's metadata extraction logic can be referenced for edge cases.

### Step 2: Extend PreprocessorContext with Metadata

**Objective:** PreprocessorContext carries metadata dictionary.
**Confidence:** High
**Depends on:** None

**Files:**
- `src/Zoh.Runtime/Preprocessing/PreprocessorContext.cs`

**Changes:**

```csharp
// Add property with empty default (backward-compatible)
public Dictionary<string, string> Metadata { get; set; } = new();
```

**Verification:** Compiles. Existing preprocessors unaffected.

### Step 3: Update EmbedPreprocessor

**Objective:** Support `${name}` interpolation and `#embed?` optional variant.
**Confidence:** High
**Depends on:** Steps 1, 2

**Files:**
- `src/Zoh.Runtime/Preprocessing/EmbedPreprocessor.cs`

**Changes:**

Update regex to match both `#embed` and `#embed?`:

```csharp
// Before:
private static readonly Regex EmbedRegex = new(@"^\s*#embed\s+""([^""]+)""\s*;", ...);

// After:
private static readonly Regex EmbedRegex = new(@"^\s*#embed(\??)\s+""([^""]+)""\s*;", ...);
```

Add interpolation method:

```csharp
private string InterpolatePath(string path, PreprocessorContext context)
{
    return Regex.Replace(path, @"\$\{(\w+)\}", match =>
    {
        var name = match.Groups[1].Value;

        // 1. Built-in vars
        if (name == "filename" && !string.IsNullOrEmpty(context.SourcePath))
            return Path.GetFileNameWithoutExtension(context.SourcePath);

        // 2. Runtime flags
        if (context.RuntimeFlags.TryGetValue(name, out var flagVal))
            return flagVal.ToString();

        // 3. Story metadata
        if (context.Metadata.TryGetValue(name, out var metaVal))
            return metaVal;

        // 4. Unknown → empty string
        return "";
    });
}
```

Update processing loop to use interpolation and handle `#embed?`:

```csharp
var isOptional = match.Groups[1].Value == "?";
var rawPath = match.Groups[2].Value;
var interpolatedPath = InterpolatePath(rawPath, context);

// Resolve file
try
{
    var content = _fileReader.Read(interpolatedPath, currentFilePath);
    // ... existing replacement logic ...
}
catch (FileNotFoundException) when (isOptional)
{
    // #embed? — silently remove the directive line
    // Replace the #embed? line with empty string
}
```

**Verification:**
- `#embed "static.zoh";` — unchanged behavior
- `#embed "${filename}.local.zoh";` — interpolates filename
- `#embed? "missing.zoh";` — silently removed
- `#embed "missing.zoh";` — still fatal

### Step 4: Register MetadataExtractor in pipeline

**Objective:** Wire MetadataExtractor into the preprocessor pipeline.
**Confidence:** High
**Depends on:** Step 1

**Files:**
- `src/Zoh.Runtime/Execution/ZohRuntime.cs` or `HandlerRegistry.cs`

**Changes:**

Register MetadataExtractor at priority 50 alongside existing preprocessors:

```csharp
RegisterPreprocessor(new MetadataExtractor()); // priority 50
// EmbedPreprocessor at 100 (existing)
// MacroPreprocessor at 200 (existing)
```

Ensure `PreprocessorContext` is shared or metadata is passed between steps — check how the pipeline passes context between preprocessors. If each preprocessor gets a fresh context, MetadataExtractor must write metadata to a shared location.

**Verification:** MetadataExtractor runs before EmbedPreprocessor. EmbedPreprocessor sees populated `context.Metadata`.

**If this fails:** If preprocessors don't share context, metadata must be extracted inline at the start of EmbedPreprocessor.Process() instead of using a separate preprocessor.

---

## Verification Plan

### Automated Checks

- [ ] `dotnet build` succeeds
- [ ] `dotnet test` — all existing embed tests pass
- [ ] New tests for interpolation (filename, flag, metadata, unknown)
- [ ] New tests for `#embed?` (missing file, existing file, circular)

### Acceptance Criteria Validation

| Criterion | How to Verify | Expected Result |
|-----------|---------------|-----------------|
| `${filename}` interpolation | Embed with `${filename}` in path | Resolves to source file base name |
| `${flag}` interpolation | Set runtime flag, embed with `${flag}` | Resolves to flag value |
| `${meta}` interpolation | Story with metadata, embed with `${meta}` | Resolves to metadata value |
| Resolution order | All three set, same name | Built-in wins |
| Unknown variable | `${nonexistent}` in path | Resolves to empty string |
| Optional embed missing | `#embed? "no_such_file.zoh";` | Directive removed, no error |
| Optional embed exists | `#embed? "real_file.zoh";` | File embedded normally |
| Static embed unchanged | `#embed "file.zoh";` | Existing behavior preserved |

---

## Rollback Plan

1. Delete MetadataExtractor.cs
2. Revert EmbedPreprocessor.cs regex and processing changes
3. Revert PreprocessorContext.cs Metadata property
4. Revert registration in pipeline

---

## Notes

### Risks

- **Metadata parsing accuracy:** The lightweight regex extraction may miss edge cases (multiline values, special characters in keys). Mitigation: only used for interpolation — full parsing still happens in Parser.
- **Context sharing:** If preprocessors don't share a PreprocessorContext instance, MetadataExtractor's output won't reach EmbedPreprocessor. Mitigation: verify pipeline architecture in Step 4; fall back to inline extraction.

### Open Questions

None.
