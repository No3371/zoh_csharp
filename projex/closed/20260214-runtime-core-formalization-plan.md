# Runtime Core Formalization

> **Status:** Complete
> **Completed:** 2026-02-15
> **Walkthrough:** [Walkthrough](file:///S:/Repos/zoh/c%23/projex/closed/20260215-runtime-core-formalization-walkthrough.md)
> **Created:** 2026-02-14
> **Author:** Agent
> **Source:** Milestone 4.1 in `20260207-csharp-runtime-nav.md`
> **Related Projex:** [Navigation](20260207-csharp-runtime-nav.md), [Red Team](../../projex/20260207-spec-impl-redteam.md)
> **Reviewed:** 2026-02-15 - [Review](20260215-runtime-core-formalization-plan-review.md)
> **Review Outcome:** Valid

---

## Summary

Refactor `ZohRuntime` from a monolithic class into a handler-registry architecture following `impl/09_runtime.md`. Introduces `RuntimeConfig`, ordered handler registries (preprocessor chain, compiler chain, story validators, verb validators), and a formal compilation pipeline. This is the architectural backbone for all subsequent Phase 4 milestones.

**Scope:** `c#/src/Zoh.Runtime/` — Execution, Verbs, Diagnostics, Preprocessing directories
**Estimated Changes:** ~10 files modified, ~5 new files, ~3 new test files

---

## Objective

### Problem / Gap / Need

The current `ZohRuntime.LoadStory()` is a monolithic method that hardcodes the pipeline: Lex → Parse → Validate → Compile. The spec (`impl/09_runtime.md`) prescribes an extensible handler-registry architecture with ordered preprocessors, compilers, story validators, verb validators, and verb drivers — all with priority-based ordering. Key gaps:

1. **No `RuntimeConfig`** — No centralized configuration for resource limits, diagnostics toggle, or asset resolution
2. **No handler registries** — Preprocessors (`EmbedPreprocessor`, `MacroPreprocessor`) exist but aren't wired into the runtime; compilers and story validators don't exist as concepts
3. **No compilation pipeline** — `LoadStory()` hardcodes each phase inline instead of dispatching to registered handlers
4. **No priority on `IVerbDriver`** — The spec requires priority-based driver selection; current interface lacks `Priority`
5. **`NamespaceValidator` operates on AST** — Should integrate into the pipeline as a story validator operating on `CompiledStory`

### Success Criteria

- [ ] `RuntimeConfig` class exists with `maxContexts`, `maxChannelDepth`, `executionTimeoutMs`, `enableDiagnostics` properties
- [ ] `HandlerRegistry` class manages ordered lists of preprocessors, compilers, story validators, verb validators, and verb drivers
- [ ] `ZohRuntime` delegates to handler registries instead of hardcoding pipeline steps
- [ ] `LoadStory()` follows the compilation pipeline: preprocess → lex → parse → compile → validate
- [ ] Preprocessors (`EmbedPreprocessor`, `MacroPreprocessor`) are wired through the registry
- [ ] `IVerbDriver` gains `Priority` property; `VerbRegistry` uses it for driver selection
- [ ] `IStoryValidator` and `IVerbValidator` interfaces exist and integrate into the pipeline
- [ ] All 515+ existing tests continue to pass
- [ ] New tests cover handler registration, pipeline execution, priority ordering, and `RuntimeConfig` defaults

### Out of Scope

- Concrete resource limit enforcement (fork bombs, channel depth — deferred to Phase 5 per nav doc)
- Storage completion (`EraseDriver`, `PurgeDriver` — Milestone 4.2)
- Validation pipeline details (duplicate labels, required verbs — Milestone 4.3)
- Standard verbs / presentation handlers (Milestones 4.4, 4.5)
- File/SQLite storage backends (Phase 5)

---

## Context

### Current State

`ZohRuntime` is a ~220-line class that owns a `VerbRegistry`, `ChannelManager`, `SignalManager`, and `InMemoryStorage`. Its `LoadStory()` method directly instantiates `Lexer`, `Parser`, `NamespaceValidator`, and calls `CompiledStory.FromAst()`. Preprocessing is entirely disconnected — `EmbedPreprocessor` and `MacroPreprocessor` implement `IPreprocessor` (which has `Priority` and `Process()`) but are never called from the runtime.

`VerbRegistry` is robust — it has suffix-indexed resolution supporting namespaced verbs with ambiguity detection. But `IVerbDriver` has no `Priority` field, and driver selection is first-match rather than highest-priority.

`NamespaceValidator` validates `StoryAst` (pre-compilation), not `CompiledStory`, using `VerbRegistry` for resolution. The spec envisions it as a story validator running post-compilation.

### Key Files

| File | Purpose | Changes Needed |
|------|---------|----------------|
| `src/Zoh.Runtime/Execution/ZohRuntime.cs` | Runtime orchestration | Refactor to use handler registries + pipeline |
| `src/Zoh.Runtime/Execution/Context.cs` | Execution context | Add `RuntimeConfig`-aware context creation |
| `src/Zoh.Runtime/Verbs/IVerbDriver.cs` | Verb driver interface | Add `Priority` property |
| `src/Zoh.Runtime/Verbs/VerbRegistry.cs` | Verb driver registry | Priority-based selection; integrate as handler |
| `src/Zoh.Runtime/Preprocessing/IPreprocessor.cs` | Preprocessor interface | Already has priority; no changes needed |
| `src/Zoh.Runtime/Validation/NamespaceValidator.cs` | Namespace validation | Adapt to `IStoryValidator` |
| `src/Zoh.Runtime/Diagnostics/DiagnosticBag.cs` | Diagnostic collection | Minor: add `HasFatalErrors` property |

### Dependencies

- **Requires:** None — this is the first milestone of Phase 4
- **Blocks:** Milestones 4.2 (Storage), 4.3 (Validation), 4.4–4.5 (Standard Verbs)

### Constraints

- Must not break any existing 515+ tests
- Must maintain backward compatibility for existing test patterns (`new ZohRuntime()` → `LoadStory()` → `CreateContext()` → `Run()`)
- Handler registries should be additive — existing code that doesn't use the new pipeline should still work

---

## Implementation

### Overview

The implementation follows a layered approach: (1) define new interfaces and `RuntimeConfig`, (2) build the `HandlerRegistry`, (3) refactor `ZohRuntime` to delegate to the registry, (4) wire existing components, (5) add tests.

### Step 1: Define `RuntimeConfig`

**Objective:** Create a configuration class for runtime limits and behavior toggles.

**Files:**
- `src/Zoh.Runtime/Execution/RuntimeConfig.cs` [NEW]

**Changes:**

```csharp
namespace Zoh.Runtime.Execution;

/// <summary>
/// Configuration for a ZOH runtime instance.
/// </summary>
public class RuntimeConfig
{
    /// <summary>Max concurrent contexts (fork limit). 0 = unlimited.</summary>
    public int MaxContexts { get; set; } = 0;
    
    /// <summary>Max channel buffer depth. 0 = unlimited.</summary>
    public int MaxChannelDepth { get; set; } = 0;
    
    /// <summary>Execution timeout in milliseconds. 0 = no timeout.</summary>
    public int ExecutionTimeoutMs { get; set; } = 0;
    
    /// <summary>Whether diagnostics are collected and accessible via /diagnose.</summary>
    public bool EnableDiagnostics { get; set; } = true;
    
    /// <summary>Default RuntimeConfig with no limits.</summary>
    public static RuntimeConfig Default => new();
}
```

**Rationale:** The spec defines `RuntimeConfig` with `assetResolver`, `maxContexts`, `executionTimeoutMs`, `enableDiagnostics`. We include `maxChannelDepth` per Phase 4 needs but defer `assetResolver` to Milestone 4.5 (media verbs). All limits default to 0/unlimited for backward compatibility — enforcement is Phase 5.

**Verification:** Compiles; tested via constructor defaults in new tests.

---

### Step 2: Define Handler Interfaces

**Objective:** Create `IStoryValidator` and `IVerbValidator` interfaces to formalize the validation extension points.

**Files:**
- `src/Zoh.Runtime/Validation/IStoryValidator.cs` [NEW]
- `src/Zoh.Runtime/Validation/IVerbValidator.cs` [NEW]

**Changes:**

```csharp
// IStoryValidator.cs
using Zoh.Runtime.Diagnostics;
using Zoh.Runtime.Execution;

namespace Zoh.Runtime.Validation;

/// <summary>
/// Validates a compiled story before execution.
/// </summary>
public interface IStoryValidator
{
    int Priority { get; }
    IReadOnlyList<Diagnostic> Validate(CompiledStory story);
}
```

```csharp
// IVerbValidator.cs
using Zoh.Runtime.Diagnostics;
using Zoh.Runtime.Execution;
using Zoh.Runtime.Parsing.Ast;

namespace Zoh.Runtime.Validation;

/// <summary>
/// Validates compiled verb calls for a specific verb.
/// </summary>
public interface IVerbValidator
{
    string VerbName { get; }
    int Priority { get; }
    IReadOnlyList<Diagnostic> Validate(VerbCallAst call, CompiledStory story);
}
```

**Rationale:** These match the spec's `StoryValidator` and `VerbValidator` handler types. `IVerbValidator.Validate` takes `VerbCallAst` because the current compiled story stores `StatementAst.VerbCall` nodes which wrap `VerbCallAst`. When compilation is formalized further (separate milestone), this can evolve.

**Verification:** Compiles; used by `HandlerRegistry` in Step 3.

---

### Step 3: Add `Priority` to `IVerbDriver`

**Objective:** Allow priority-based verb driver selection per the spec.

**Files:**
- `src/Zoh.Runtime/Verbs/IVerbDriver.cs`

**Changes:**

```diff
 public interface IVerbDriver
 {
     string Namespace { get; }
     string Name { get; }
+    /// <summary>
+    /// Priority for driver selection. Lower values = higher priority.
+    /// Core built-in handlers: int.MinValue to 0.
+    /// High-priority extensions: 1–1000.
+    /// Standard extensions: 1001–10000.
+    /// Low-priority extensions: 10001+.
+    /// </summary>
+    int Priority => 0; // Default implementation for backward compat
 
     VerbResult Execute(IExecutionContext context, VerbCallAst verbCall);
 }
```

**Rationale:** Uses a default interface implementation (`=> 0`) so all existing drivers automatically get priority 0 (core range) without any code changes. This is non-breaking.

**Verification:** All existing tests pass. Priority used by `VerbRegistry.GetDriver()` in Step 5.

---

### Step 4: Build `HandlerRegistry`

**Objective:** Create a unified registry that manages all handler types with priority ordering.

**Files:**
- `src/Zoh.Runtime/Execution/HandlerRegistry.cs` [NEW]

**Changes:**

```csharp
using Zoh.Runtime.Preprocessing;
using Zoh.Runtime.Validation;
using Zoh.Runtime.Verbs;

namespace Zoh.Runtime.Execution;

/// <summary>
/// Central registry for all runtime handler chains.
/// Handlers are stored ordered by priority (ascending = runs first).
/// </summary>
public class HandlerRegistry
{
    private readonly List<IPreprocessor> _preprocessors = new();
    private readonly List<IStoryValidator> _storyValidators = new();
    private readonly Dictionary<string, List<IVerbValidator>> _verbValidators = new(StringComparer.OrdinalIgnoreCase);
    
    public VerbRegistry VerbDrivers { get; } = new();
    
    // --- Preprocessors ---
    
    public IReadOnlyList<IPreprocessor> Preprocessors => _preprocessors;
    
    public void RegisterPreprocessor(IPreprocessor preprocessor)
    {
        _preprocessors.Add(preprocessor);
        _preprocessors.Sort((a, b) => a.Priority.CompareTo(b.Priority));
    }
    
    // --- Story Validators ---
    
    public IReadOnlyList<IStoryValidator> StoryValidators => _storyValidators;
    
    public void RegisterStoryValidator(IStoryValidator validator)
    {
        _storyValidators.Add(validator);
        _storyValidators.Sort((a, b) => a.Priority.CompareTo(b.Priority));
    }
    
    // --- Verb Validators ---
    
    public IReadOnlyDictionary<string, List<IVerbValidator>> VerbValidators => _verbValidators;
    
    public void RegisterVerbValidator(IVerbValidator validator)
    {
        var key = validator.VerbName.ToLowerInvariant();
        if (!_verbValidators.TryGetValue(key, out var list))
        {
            list = new List<IVerbValidator>();
            _verbValidators[key] = list;
        }
        list.Add(validator);
        list.Sort((a, b) => a.Priority.CompareTo(b.Priority));
    }
    
    /// <summary>
    /// Registers all core verb drivers and wires preprocessors/validators.
    /// </summary>
    public void RegisterCoreHandlers()
    {
        VerbDrivers.RegisterCoreVerbs();
    }
}
```

**Rationale:** The spec shows separate registries for each handler type. Consolidating them into a single `HandlerRegistry` simplifies the runtime surface while keeping ordered lists for each concern. `VerbRegistry` is reused as-is for verb drivers (it already has suffix matching and resolution). Priority sorting is ascending (lower = runs first) matching the spec's convention where built-in handlers use negative/zero priorities.

**Verification:** Unit tests for registration ordering.

---

### Step 5: Refactor `ZohRuntime` to Use Handler Registry and Pipeline

**Objective:** Replace monolithic `LoadStory()` with a pipeline that dispatches to registered handlers.

**Files:**
- `src/Zoh.Runtime/Execution/ZohRuntime.cs`

**Changes:**

The core refactoring:

```csharp
public class ZohRuntime
{
    public RuntimeConfig Config { get; }
    public HandlerRegistry Handlers { get; }
    public ChannelManager Channels { get; } = new();
    public SignalManager SignalManager { get; } = new();
    public Storage.IPersistentStorage Storage { get; set; }
    
    // Backward compat: keep VerbRegistry accessor
    public VerbRegistry VerbRegistry => Handlers.VerbDrivers;
    
    public IReadOnlyList<Context> Contexts => _contexts;
    private readonly List<Context> _contexts = new();
    private readonly Dictionary<string, CompiledStory> _storyCache = new();
    
    public ZohRuntime() : this(RuntimeConfig.Default) { }
    
    public ZohRuntime(RuntimeConfig config)
    {
        Config = config;
        Storage = new Storage.InMemoryStorage();
        Handlers = new HandlerRegistry();
        Handlers.RegisterCoreHandlers();
    }
    
    /// <summary>
    /// Loads a story through the compilation pipeline:
    /// preprocess → lex → parse → compile → validate.
    /// </summary>
    public CompiledStory LoadStory(string source, string sourcePath = "")
    {
        var diagnostics = new DiagnosticBag();
        
        // 1. Preprocess
        string processed = source;
        foreach (var pp in Handlers.Preprocessors)
        {
            var ctx = new PreprocessorContext(processed, sourcePath);
            var result = pp.Process(ctx);
            diagnostics.AddRange(result.Diagnostics);
            if (diagnostics.HasFatalErrors)
                throw new CompilationException("Preprocessing failed", diagnostics);
            processed = result.ProcessedText;
        }
        
        // 2. Lex
        var lexer = new Lexer(processed, true);
        var tokens = lexer.Tokenize();
        if (tokens.HasErrors)
            throw new CompilationException("Lexing failed: " + string.Join(", ", tokens.Errors), diagnostics);
        
        // 3. Parse
        var parser = new Parser(tokens.Tokens);
        var parseResult = parser.Parse();
        if (!parseResult.Success)
            throw new CompilationException("Parsing failed: " + string.Join(", ", parseResult.Errors), diagnostics);
        
        // 4. Compile (currently: wrap AST)
        var compiled = CompiledStory.FromAst(parseResult.Story!);
        
        // 5. Validate
        // 5a. Story validators
        foreach (var validator in Handlers.StoryValidators)
        {
            var valDiags = validator.Validate(compiled);
            diagnostics.AddRange(valDiags);
        }
        
        // 5b. Namespace validation (built-in, always runs)
        var nsValidator = new NamespaceValidator(VerbRegistry);
        var nsResult = nsValidator.Validate(parseResult.Story!);
        if (!nsResult.IsSuccess)
            throw new CompilationException("Validation failed: " + string.Join(", ", nsResult.Errors.Select(e => e.Message)), diagnostics);
        
        // 5c. Check for fatal diagnostics from story validators
        if (diagnostics.HasFatalErrors)
            throw new CompilationException("Validation failed", diagnostics);
        
        _storyCache[compiled.Name] = compiled;
        return compiled;
    }
    
    // Run(), ExecuteVerb(), CreateContext(), AddContext() remain unchanged
}
```

Key design decisions:
- **Parameterless constructor preserved** — `new ZohRuntime()` still works, using `RuntimeConfig.Default`
- **New overload** — `ZohRuntime(RuntimeConfig)` for custom configuration
- **`LoadStory` gains `sourcePath`** — Optional parameter for preprocessor context (file resolution). Defaults to empty string for backward compat.
- **`NamespaceValidator` runs as built-in** — It currently validates against `StoryAst`; converting it to `IStoryValidator` (which takes `CompiledStory`) is a Milestone 4.3 concern. For now it stays hardcoded.
- **`Storage` becomes settable** — Allows hosts to inject custom storage implementations.

**Rationale:** This refactoring is incremental. The pipeline exists and dispatches to registered handlers, but the compile phase remains simple (`FromAst`). This matches "formalize the pipeline" without requiring a full compiler handler system — the `ICompiler` interface concept from the spec can be deferred since compilation currently does nothing beyond AST wrapping.

**Verification:** All existing tests pass. New pipeline tests.

---

### Step 6: Add `CompilationException`

**Objective:** Provide a rich exception for pipeline failures that carries diagnostics.

**Files:**
- `src/Zoh.Runtime/Execution/CompilationException.cs` [NEW]

**Changes:**

```csharp
using Zoh.Runtime.Diagnostics;

namespace Zoh.Runtime.Execution;

/// <summary>
/// Thrown when the compilation pipeline fails.
/// </summary>
public class CompilationException : Exception
{
    public DiagnosticBag Diagnostics { get; }
    
    public CompilationException(string message, DiagnosticBag diagnostics)
        : base(message)
    {
        Diagnostics = diagnostics;
    }
}
```

**Rationale:** Currently `LoadStory()` throws bare `Exception`. A typed exception with attached diagnostics gives callers structured error information.

**Verification:** Existing tests that catch exceptions still work (CompilationException inherits Exception).

---

### Step 7: Add `HasFatalErrors` to `DiagnosticBag`

**Objective:** Convenience property for pipeline abort checks.

**Files:**
- `src/Zoh.Runtime/Diagnostics/DiagnosticBag.cs`

**Changes:**

```diff
   public bool HasErrors => _diagnostics.Any(d => d.Severity >= DiagnosticSeverity.Error);
+  public bool HasFatalErrors => _diagnostics.Any(d => d.Severity == DiagnosticSeverity.Fatal);
+  public int Count => _diagnostics.Count;
```

**Rationale:** `HasErrors` already exists. Pipeline needs distinct fatal check since the spec says INFO/WARNING/ERROR continue but FATAL aborts.

**Verification:** Trivial property; tested via existing DiagnosticBag usage.

---

### Step 8: Write Tests

**Objective:** Comprehensive test coverage for the new architecture.

**Files:**
- `tests/Zoh.Tests/Execution/RuntimeConfigTests.cs` [NEW]
- `tests/Zoh.Tests/Execution/HandlerRegistryTests.cs` [NEW]
- `tests/Zoh.Tests/Execution/CompilationPipelineTests.cs` [NEW]

**Changes:**

**RuntimeConfigTests.cs** — Tests for `RuntimeConfig`:
- Default values (all 0/unlimited, diagnostics enabled)
- Custom values via property setters
- `RuntimeConfig.Default` static accessor

**HandlerRegistryTests.cs** — Tests for `HandlerRegistry`:
- Preprocessor registration and priority ordering
- Story validator registration and priority ordering
- Verb validator registration keyed by verb name
- `RegisterCoreHandlers()` populates verb drivers

**CompilationPipelineTests.cs** — Tests for refactored `ZohRuntime.LoadStory()`:
- Basic pipeline: source → compiled story (existing pattern, regression)
- Pipeline with custom preprocessor registered (transforms source before lex)
- Pipeline with custom story validator registered (diagnostics collected)
- Pipeline with fatal story validator (aborts with `CompilationException`)
- `ZohRuntime(RuntimeConfig)` constructor stores config
- Backward compat: all existing `RuntimeTests` patterns still work

**Verification:** `dotnet test` passes with all new + existing tests.

---

## Verification Plan

### Automated Checks

- [ ] `cd c#; dotnet build` — Solution compiles without errors
- [ ] `cd c#; dotnet test` — All 515+ existing tests pass (regression)
- [ ] `cd c#; dotnet test --filter "FullyQualifiedName~RuntimeConfigTests"` — New config tests pass
- [ ] `cd c#; dotnet test --filter "FullyQualifiedName~HandlerRegistryTests"` — New registry tests pass
- [ ] `cd c#; dotnet test --filter "FullyQualifiedName~CompilationPipelineTests"` — New pipeline tests pass

### Acceptance Criteria Validation

| Criterion | How to Verify | Expected Result |
|-----------|---------------|-----------------|
| `RuntimeConfig` exists with required properties | `RuntimeConfigTests` | All default values correct |
| `HandlerRegistry` manages handlers | `HandlerRegistryTests` | Registration + ordering works |
| `LoadStory()` uses pipeline | `CompilationPipelineTests` | Custom preprocessors/validators invoked |
| Existing tests unbroken | `dotnet test` full suite | 515+ tests pass |
| `IVerbDriver` has `Priority` | Compiles with default impl | No existing driver changes needed |

---

## Rollback Plan

If implementation fails or causes issues:

1. Git revert the ephemeral branch — all changes are isolated
2. The plan stays in `projex/` for future re-attempt

---

## Notes

### Assumptions

- The `ICompiler` handler interface from `impl/09_runtime.md` is **deferred** — current compilation is trivial (AST wrapping). Adding a compiler chain is unnecessary complexity until compilation does actual optimization or transformation.
- `NamespaceValidator` remains hardcoded in the pipeline (validates `StoryAst`) until Milestone 4.3 converts it to an `IStoryValidator` that validates `CompiledStory`.
- Resource limit enforcement (e.g., rejecting `CreateContext()` when `MaxContexts` reached) is **deferred to Phase 5** — the config properties exist as a foundation but aren't enforced yet.

### Risks

- **Backward compat breakage**: Mitigated by preserving parameterless constructor and keeping `VerbRegistry` property accessor.
- **`Priority` default interface member**: Requires C# 8+. The project already targets a compatible framework.

### Open Questions

- (none — all questions resolved during research)
