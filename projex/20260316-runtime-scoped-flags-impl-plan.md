# Runtime-Scoped Flags — C# Implementation

> **Status:** Ready
> **Created:** 2026-03-16
> **Author:** Claude
> **Source:** Spec commits `ce45ab7`, `da860b8`, `04b0997`; impl spec `09_runtime.md`
> **Related Projex:** `20260316-spec-catchup-followup.md`, `20260316-embed-variable-interpolation-impl-plan.md`
> **Worktree:** Yes

---

## Summary

Implement the runtime-scoped flag system in the C# runtime: dual-scope storage (runtime + context), `/flag` verb driver, flag resolution chain, and runtime flag access for preprocessors.

**Scope:** Flag storage, `/flag` verb, flag resolution, preprocessor flag passthrough
**Estimated Changes:** 6 files modified, 2 files created

---

## Objective

### Problem / Gap / Need

The spec defines a flag system (spec `1_concepts.md`, impl `09_runtime.md`) with runtime-scoped and context-scoped flags. Flags configure verb driver behavior (e.g., `interactive`, `instant`, `pace`, `locale`). The C# runtime has no flag support — no storage, no verb, no resolution, no preprocessor access.

### Success Criteria

- [ ] `ZohRuntime` has `flags: Dictionary<string, ZohValue>` with `SetFlag`/`GetFlag` API
- [ ] `Context` has `flags: Dictionary<string, ZohValue>` copied on fork
- [ ] `/flag` verb driver sets context-scoped flags by default, runtime-scoped with `[scope: "runtime"]`
- [ ] Flag resolution: context → runtime → null
- [ ] `IPreprocessor.Process` receives runtime flags
- [ ] `dotnet test` passes

### Out of Scope

- Standard flag defaults (`interactive`, `instant`, `pace`, `locale`) — those are host-level concerns
- Flag validation (type checking against std_flags.md)
- Embed path interpolation using flags (separate plan)

---

## Context

### Current State

`ZohRuntime` (`src/Zoh.Runtime/Execution/ZohRuntime.cs`) has no `flags` field. `Context` (`src/Zoh.Runtime/Execution/Context.cs`) has no `flags` field. `IPreprocessor.Process` takes a `PreprocessorContext` with only `SourceText` and `SourcePath`. No `/flag` verb driver exists.

### Key Files

| File | Role | Change Summary |
|------|------|----------------|
| `src/Zoh.Runtime/Execution/ZohRuntime.cs` | Runtime state | Add `_flags` dict, `SetFlag`/`GetFlag` methods |
| `src/Zoh.Runtime/Execution/Context.cs` | Context state | Add `_flags` dict, `GetFlag` with resolution chain |
| `src/Zoh.Runtime/Execution/IExecutionContext.cs` | Verb driver interface | Add `GetFlag`/`SetFlag` surface |
| `src/Zoh.Runtime/Verbs/Core/FlagDriver.cs` | New: `/flag` verb | Implement flag verb with scope attribute |
| `src/Zoh.Runtime/Preprocessing/PreprocessorContext.cs` | Preprocessor input | Add `RuntimeFlags` property |
| `src/Zoh.Runtime/Execution/HandlerRegistry.cs` | Verb registration | Register FlagDriver |

### Dependencies

- **Requires:** None
- **Blocks:** `20260316-embed-variable-interpolation-impl-plan.md` (needs runtime flags in preprocessor)

### Constraints

- Flag resolution mirrors variable scoping: context shadows runtime (spec `impl/09_runtime.md`)
- Context flags copied to forked contexts (same as variables)
- Runtime flags visible to preprocessors (passed through `PreprocessorContext`)

### Assumptions

- `IExecutionContext` can be extended without breaking existing verb drivers (additive change)
- `PreprocessorContext` can gain new properties with defaults (backward-compatible)
- Fork/call already copies fields from parent context — adding flags follows the same pattern

### Impact Analysis

- **Direct:** ZohRuntime, Context, IExecutionContext, PreprocessorContext
- **Adjacent:** ForkDriver/CallDriver (need to copy context flags), preprocessor pipeline in ZohRuntime.LoadStory
- **Downstream:** Future embed interpolation, future `#strsub` preprocessor

---

## Implementation

### Overview

Six steps: (1) add flag storage to ZohRuntime, (2) add flag storage to Context with resolution, (3) surface flags on IExecutionContext, (4) create FlagDriver, (5) extend PreprocessorContext and wire in LoadStory, (6) copy flags in fork/call.

### Step 1: Add flag storage to ZohRuntime

**Objective:** Store runtime-scoped flags with public get/set API.
**Confidence:** High
**Depends on:** None

**Files:**
- `src/Zoh.Runtime/Execution/ZohRuntime.cs`

**Changes:**

Add a private dictionary field and public methods:

```csharp
// Add field alongside existing fields (channelHubs, storage, signals, etc.)
private readonly Dictionary<string, ZohValue> _flags = new();

// Public API
public void SetFlag(string name, ZohValue value) => _flags[name] = value;
public ZohValue? GetFlag(string name) => _flags.TryGetValue(name, out var v) ? v : null;
internal IReadOnlyDictionary<string, ZohValue> Flags => _flags;
```

**Verification:** Compiles. `Flags` property accessible from Context and LoadStory.

### Step 2: Add flag storage to Context with resolution chain

**Objective:** Context-scoped flags with resolution: context → runtime → null.
**Confidence:** High
**Depends on:** Step 1

**Files:**
- `src/Zoh.Runtime/Execution/Context.cs`

**Changes:**

Add flag dictionary and resolution method:

```csharp
// Field
private readonly Dictionary<string, ZohValue> _flags = new();

// Context-level set (used by FlagDriver for default scope)
public void SetContextFlag(string name, ZohValue value) => _flags[name] = value;

// Resolution chain: context → runtime → null
public ZohValue? ResolveFlag(string name)
{
    if (_flags.TryGetValue(name, out var v)) return v;
    return Runtime.GetFlag(name);
}
```

Where `Runtime` is the `ZohRuntime` reference already held by Context.

**Verification:** Resolution returns context flag when set, falls through to runtime flag, returns null when neither set.

### Step 3: Surface flags on IExecutionContext

**Objective:** Verb drivers can read and write flags through the execution context interface.
**Confidence:** High
**Depends on:** Step 2

**Files:**
- `src/Zoh.Runtime/Execution/IExecutionContext.cs`

**Changes:**

Add to interface:

```csharp
ZohValue? ResolveFlag(string name);
void SetContextFlag(string name, ZohValue value);
ZohRuntime Runtime { get; }  // If not already exposed — needed for [scope: "runtime"]
```

Context already implements these from Step 2. If `Runtime` is already accessible through another path (check existing interface), use that instead.

**Verification:** FlagDriver (Step 4) can call `context.ResolveFlag` and `context.SetContextFlag`.

**If this fails:** If `Runtime` can't be exposed on IExecutionContext, FlagDriver can use `((Context)context).Runtime.SetFlag(...)` — less clean but functional.

### Step 4: Create FlagDriver

**Objective:** Implement `/flag` verb: `/flag "name", value;` with optional `[scope: "runtime"]`.
**Confidence:** High
**Depends on:** Step 3

**Files:**
- `src/Zoh.Runtime/Verbs/Core/FlagDriver.cs` (new)
- `src/Zoh.Runtime/Execution/HandlerRegistry.cs`

**Changes:**

New `FlagDriver.cs`:

```csharp
public class FlagDriver : IVerbDriver
{
    public string Namespace => "core";
    public string Name => "flag";

    public DriverResult Execute(IExecutionContext context, VerbCallAst verb)
    {
        // /flag "name", value;
        // [scope: "runtime"] — write to runtime scope instead of context
        if (verb.UnnamedParams.Length < 2)
            return DriverResult.Complete.Fatal(
                new Diagnostic(DiagnosticSeverity.Fatal, "parameter_not_found",
                    "Usage: /flag \"name\", value;", verb.Start));

        var nameVal = ValueResolver.Resolve(verb.UnnamedParams[0], context);
        if (nameVal is not ZohStr nameStr)
            return DriverResult.Complete.Fatal(
                new Diagnostic(DiagnosticSeverity.Fatal, "invalid_type",
                    "Flag name must be a string", verb.Start));

        var value = ValueResolver.Resolve(verb.UnnamedParams[1], context);

        var scopeAttr = verb.Attributes
            .FirstOrDefault(a => a.Name.Equals("scope", StringComparison.OrdinalIgnoreCase));
        bool isRuntime = false;
        if (scopeAttr?.Value != null)
        {
            var scopeVal = ValueResolver.Resolve(scopeAttr.Value, context);
            isRuntime = scopeVal is ZohStr s &&
                        s.Value.Equals("runtime", StringComparison.OrdinalIgnoreCase);
        }

        if (isRuntime)
            context.Runtime.SetFlag(nameStr.Value, value);
        else
            context.SetContextFlag(nameStr.Value, value);

        return DriverResult.Complete.Ok(value);
    }
}
```

Register in `HandlerRegistry.RegisterCoreHandlers()`:

```csharp
RegisterVerbDriver(new FlagDriver());
```

**Verification:** `/flag "locale", "en";` sets context flag. `/flag [scope: "runtime"] "locale", "en";` sets runtime flag. `context.ResolveFlag("locale")` returns the value.

### Step 5: Pass runtime flags to preprocessor pipeline

**Objective:** Preprocessors receive runtime flags via PreprocessorContext.
**Confidence:** High
**Depends on:** Step 1

**Files:**
- `src/Zoh.Runtime/Preprocessing/PreprocessorContext.cs` (or wherever this type lives)
- `src/Zoh.Runtime/Execution/ZohRuntime.cs` (LoadStory method)

**Changes:**

Extend `PreprocessorContext`:

```csharp
// Add property with empty default for backward compatibility
public IReadOnlyDictionary<string, ZohValue> RuntimeFlags { get; init; }
    = new Dictionary<string, ZohValue>();
```

In `ZohRuntime.LoadStory`, populate when creating PreprocessorContext:

```csharp
var ctx = new PreprocessorContext(processed, sourcePath)
{
    RuntimeFlags = _flags
};
```

**Verification:** Preprocessors can read `context.RuntimeFlags` during processing. Existing preprocessors unaffected (they don't read it).

### Step 6: Copy context flags on fork/call

**Objective:** Forked contexts inherit parent's context-scoped flags.
**Confidence:** High
**Depends on:** Step 2

**Files:**
- `src/Zoh.Runtime/Execution/Context.cs` (or ForkDriver/CallDriver, depending on where context cloning happens)

**Changes:**

Find where forked contexts are created (likely in ForkDriver or Context.Clone). Copy `_flags` dictionary:

```csharp
// In fork/clone logic
foreach (var kvp in parent._flags)
    child._flags[kvp.Key] = kvp.Value;
```

**Verification:** Flag set in parent context visible in forked context. Forked context can shadow parent flags independently.

---

## Verification Plan

### Automated Checks

- [ ] `dotnet build` succeeds
- [ ] `dotnet test` — all existing tests pass
- [ ] New unit tests for FlagDriver (context scope, runtime scope, resolution chain)
- [ ] New unit test: fork copies context flags

### Manual Verification

- [ ] `/flag "test", 42;` → `context.ResolveFlag("test")` returns `42`
- [ ] `/flag [scope: "runtime"] "locale", "en";` → `runtime.GetFlag("locale")` returns `"en"`
- [ ] Context flag shadows runtime flag of same name
- [ ] Forked context inherits parent flags

### Acceptance Criteria Validation

| Criterion | How to Verify | Expected Result |
|-----------|---------------|-----------------|
| Runtime flag storage | `runtime.SetFlag` + `runtime.GetFlag` | Value round-trips |
| Context flag storage | `context.SetContextFlag` + `context.ResolveFlag` | Value round-trips |
| Resolution chain | Set runtime flag, no context flag | `ResolveFlag` returns runtime value |
| Resolution shadowing | Set both, context differs | `ResolveFlag` returns context value |
| Preprocessor access | Check `PreprocessorContext.RuntimeFlags` in LoadStory | Contains runtime flags |

---

## Rollback Plan

1. Revert FlagDriver.cs (new file — delete)
2. Revert additions to ZohRuntime, Context, IExecutionContext, PreprocessorContext, HandlerRegistry
3. All changes are additive — no existing behavior modified

---

## Notes

### Risks

- **IExecutionContext surface area:** Adding `Runtime` property may expose too much. Mitigation: expose only `SetFlag`/`GetFlag` on a narrower interface if needed.
- **Thread safety:** Flags are mutable dictionaries. Mitigation: ZOH execution is single-threaded per runtime tick — no concurrent mutation.

### Open Questions

None.
