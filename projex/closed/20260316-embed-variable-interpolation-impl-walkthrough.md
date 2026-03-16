# Walkthrough: Embed Variable Interpolation & Optional #embed? — C# Implementation

> **Execution Date:** 2026-03-16
> **Completed By:** Claude
> **Source Plan:** `20260316-embed-variable-interpolation-impl-plan.md`
> **Result:** Success

---

## Summary

Added `${name}` path interpolation and `#embed?` optional variant to `EmbedPreprocessor`. Interpolation resolves from built-in vars, runtime flags, and story metadata in that order. All 654 tests pass.

---

## Objectives Completion

| Objective | Status | Notes |
|-----------|--------|-------|
| `${filename}` interpolation | Complete | Resolves to source file base name via `Path.GetFileNameWithoutExtension` |
| `${flag}` interpolation | Complete | Resolves from `context.RuntimeFlags` |
| `${meta}` interpolation | Complete | Resolves from `context.Metadata` (inline extraction from story header) |
| Resolution order built-in → flag → metadata → empty | Complete | Implemented in `InterpolatePath` |
| `#embed?` skips silently on file not found | Complete | `catch (FileNotFoundException) when (isOptional)` |
| `#embed?` still fatal on circular embed | Complete | Circular check runs before file read |
| Static `#embed` behavior unchanged | Complete | Regression test confirms |
| `dotnet test` passes | Complete | 654/654 |

---

## Execution Detail

### Step 1: Extend PreprocessorContext (Plan Step 2)

**Planned:** Add `Dictionary<string, string> Metadata { get; set; } = new();` to `PreprocessorContext` in its own `PreprocessorContext.cs`.

**Actual:** Added `RuntimeFlags` and `Metadata` properties directly to `PreprocessorContext` in `IPreprocessor.cs` (where the class is currently defined — no separate file exists).

**Deviation:** Also added `RuntimeFlags` property, which the plan assumed would be provided by the dependency plan (`20260316-runtime-scoped-flags-impl-plan.md`). Since that plan is still in progress and hasn't added it yet, it was added here with an empty default so interpolation compiles and works. The flags plan can populate it when it lands.

**Files Changed:**
| File | Change Type | Planned? | Details |
|------|-------------|----------|---------|
| `src/Zoh.Runtime/Preprocessing/IPreprocessor.cs` | Modified | Yes | Lines 32–37: added `RuntimeFlags` and `Metadata` settable properties with empty defaults |

---

### Step 2: MetadataExtractor preprocessor (Plan Step 1) → Inlined

**Planned:** Create a separate `MetadataExtractor` preprocessor at priority 50, registered in `HandlerRegistry`.

**Actual:** Extracted metadata inline at the start of `EmbedPreprocessor.Process()` via a private static `ExtractMetadata(context)` method. No separate preprocessor created, no registration change.

**Deviation:** The plan anticipated this fallback in Step 4: "If preprocessors don't share context, metadata must be extracted inline at the start of EmbedPreprocessor.Process() instead." Confirmed: `ZohRuntime.LoadStory` creates a fresh `PreprocessorContext` for each preprocessor, so a separate `MetadataExtractor` could not pass metadata to `EmbedPreprocessor`. Inline extraction is the correct approach.

**Files Changed:**
| File | Change Type | Planned? | Details |
|------|-------------|----------|---------|
| `src/Zoh.Runtime/Preprocessing/MetadataExtractor.cs` | Not Created | Was planned | Replaced by inline method |
| `src/Zoh.Runtime/Execution/HandlerRegistry.cs` | Not Modified | Was planned | No registration needed without separate preprocessor |

---

### Step 3: Update EmbedPreprocessor (Plan Step 3)

**Planned:** Update regex to `(\??)` capture group, add `InterpolatePath`, handle `#embed?` with `catch (FileNotFoundException) when (isOptional)`.

**Actual:** Implemented exactly as planned, plus added a second compiled static regex `InterpolationRegex` for the `${name}` pattern to avoid repeated `new Regex(...)` calls in `InterpolatePath`.

**Deviation:** Minor — extracted `InterpolationRegex` as a static field for efficiency, not specified in plan.

**Files Changed:**
| File | Change Type | Planned? | Details |
|------|-------------|----------|---------|
| `src/Zoh.Runtime/Preprocessing/EmbedPreprocessor.cs` | Modified | Yes | Regex updated (ln 14), `InterpolationRegex` added (ln 15), `Process()` calls `ExtractMetadata` and passes context to `ProcessRecursive`, `ProcessRecursive` signature extended with `context` param, `InterpolatePath` and `ExtractMetadata` added as static methods |

---

### Step 4: Tests

**Planned:** New tests for interpolation (filename, flag, metadata, unknown) and `#embed?` (missing file, existing file, circular).

**Actual:** 8 new tests added covering all planned scenarios plus a static regression test.

**Files Changed:**
| File | Change Type | Planned? | Details |
|------|-------------|----------|---------|
| `tests/Zoh.Tests/Preprocessing/PreprocessorTests.cs` | Modified | Yes | 8 new test methods added (ln 181–297 before existing macro tests) |

---

## Complete Change Log

### Files Created
None.

### Files Modified
| File | Changes | In Plan? |
|------|---------|----------|
| `src/Zoh.Runtime/Preprocessing/IPreprocessor.cs` | Added `RuntimeFlags` and `Metadata` properties to `PreprocessorContext` | Yes (Metadata); RuntimeFlags added as dependency stub |
| `src/Zoh.Runtime/Preprocessing/EmbedPreprocessor.cs` | Regex, interpolation, `#embed?`, inline metadata extraction | Yes |
| `tests/Zoh.Tests/Preprocessing/PreprocessorTests.cs` | 8 new test methods | Yes |

### Files Deleted
None.

### Planned But Not Changed
| File | Planned Change | Why Not Done |
|------|----------------|--------------|
| `src/Zoh.Runtime/Preprocessing/MetadataExtractor.cs` | Create new preprocessor | Fresh-context architecture makes separate preprocessor unable to share data; inlined instead per plan's own fallback |
| `src/Zoh.Runtime/Execution/HandlerRegistry.cs` | Register MetadataExtractor | Not needed without separate preprocessor |

---

## Success Criteria Verification

| Criterion | Method | Result | Evidence |
|-----------|--------|--------|----------|
| `${filename}` interpolation | `Embed_InterpolatesFilename` test | PASS | 22/22 preprocessor tests |
| `${flag}` interpolation | `Embed_InterpolatesRuntimeFlag` test | PASS | 22/22 preprocessor tests |
| `${meta}` interpolation | `Embed_InterpolatesMetadata` test | PASS | 22/22 preprocessor tests |
| Resolution order | `Embed_ResolutionOrder_BuiltinBeforeFlag` test | PASS | 22/22 preprocessor tests |
| Unknown variable → empty | `Embed_UnknownVariable_ResolvesToEmpty` test | PASS | 22/22 preprocessor tests |
| `#embed?` silently skips missing | `EmbedOptional_SilentlySkips_WhenFileMissing` test | PASS | 22/22 preprocessor tests |
| `#embed?` embeds when file exists | `EmbedOptional_Embeds_WhenFileExists` test | PASS | 22/22 preprocessor tests |
| `#embed?` fatal on circular | `EmbedOptional_StillFatal_OnCircularDependency` test | PASS | 22/22 preprocessor tests |
| Static embed unchanged | `Embed_Static_BehaviorUnchanged` test | PASS | 22/22 preprocessor tests |
| `dotnet test` passes | Full suite run | PASS | 654/654 |

**Overall: 10/10 criteria passed**

---

## Deviations from Plan

### Deviation 1: MetadataExtractor inlined
- **Planned:** Separate `MetadataExtractor` class at priority 50, registered in pipeline
- **Actual:** `ExtractMetadata(context)` static method called at top of `EmbedPreprocessor.Process()`
- **Reason:** `ZohRuntime.LoadStory` creates `new PreprocessorContext(processed, sourcePath)` per preprocessor — context is not shared between passes. The plan's own Step 4 fallback anticipated exactly this.
- **Impact:** None — same behavior, simpler implementation, one fewer file
- **Recommendation:** Update plan's Step 1 and 4 notes to reflect this as the canonical approach

### Deviation 2: RuntimeFlags added here (not from dependency plan)
- **Planned:** Assumes `RuntimeFlags` already exists from `20260316-runtime-scoped-flags-impl-plan.md`
- **Actual:** Added as `Dictionary<string, string> RuntimeFlags { get; set; } = new()` stub
- **Reason:** Dependency plan still in progress, hasn't touched `PreprocessorContext` yet
- **Impact:** Forward-compatible — flags plan can populate the property from runtime config when it lands
- **Recommendation:** Flags plan should check if `RuntimeFlags` already exists before adding it

---

## Key Insights

### Lessons Learned

1. **Preprocessor context is not shared between pipeline passes**
   - `ZohRuntime.LoadStory` creates a fresh `PreprocessorContext` per preprocessor. Any plan that expects preprocessors to share state via context must either inline that logic or refactor the pipeline.

2. **Plan fallbacks are worth reading before starting**
   - The plan explicitly noted the context-sharing risk and provided the inline fallback. Reading Step 4's "If this fails" ahead of time allowed a direct correct implementation.

### Gotchas / Pitfalls

1. **`#embed?` circular check must precede file read**
   - The `embeddedFiles.Contains(absPath)` check runs before `ReadAllText`. Since `FileNotFoundException` is only caught for optional embeds, circular detection must happen first — otherwise an optional embed of a circular file would silently do nothing instead of reporting PRE001.

---

## Recommendations

### Immediate Follow-ups
- [ ] `20260316-runtime-scoped-flags-impl-plan.md` — when adding `RuntimeFlags` to `PreprocessorContext`, check that the property already exists (added here)
- [ ] `ZohRuntime.LoadStory` — when flags plan lands, populate `ctx.RuntimeFlags` from `RuntimeConfig` before calling `pp.Process(ctx)`
