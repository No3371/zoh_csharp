# Storage Completion Plan

> **Status:** Complete
> **Completed:** 2026-02-15
> **Walkthrough:** `20260214-storage-completion-walkthrough.md`
> **Created:** 2026-02-14
> **Author:** Agent
> **Source:** Navigation roadmap item 4.2 (`20260207-csharp-runtime-nav.md`)
> **Related Projex:** `20260207-csharp-runtime-nav.md`

---

## Summary

Implement the two missing storage verb drivers (`EraseDriver`, `PurgeDriver`), fix a type-check omission in `WriteDriver`, and add comprehensive test coverage for all storage verbs — including type-rejection, named stores, and round-trip scenarios.

**Scope:** `c#/src/Zoh.Runtime/Verbs/Store/` drivers and `c#/tests/Zoh.Tests/Verbs/Store/` tests
**Estimated Changes:** 3 modified files, 2 new files

---

## Objective

### Problem / Gap / Need

The navigation roadmap (item 4.2) requires completing the persistent storage subsystem. The `IPersistentStorage` interface and `InMemoryStorage` backend already support all five operations (Write, Read, Erase, Purge, Exists), but:

1. **No `EraseDriver`** — The `/erase` verb has no verb driver, so it cannot be invoked from ZOH scripts
2. **No `PurgeDriver`** — The `/purge` verb has no verb driver either
3. **`WriteDriver` missing `ZohExpr` check** — The spec mandates rejecting `expression` type alongside `verb` and `channel`, but the current code only checks `ZohVerb` and `ZohChannel`
4. **Insufficient test coverage** — Existing tests cover write/read/default/required/round-trip but not erase, purge, type rejection, named stores, or all serializable types

### Success Criteria

- [ ] `/erase store:"name"?, *var;` works correctly — removes variable from storage with diagnostic on not-found
- [ ] `/purge store:"name"?;` works correctly — clears entire store
- [ ] `/write` rejects `ZohExpr` values with fatal diagnostic (existing `ZohVerb` and `ZohChannel` checks preserved)
- [ ] All storage verb drivers registered in `VerbRegistry.RegisterCoreVerbs()`
- [ ] Tests cover all items from `impl/11_storage.md` testing checklist (basic ops, repeating params, type handling, attributes)
- [ ] `dotnet test` passes with zero failures

### Out of Scope

- Serialization format (JSON-based encoding/decoding) — deferred; current `InMemoryStorage` stores `ZohValue` directly
- File-based or SQLite storage backends — deferred to Phase 5
- Concurrency stress tests — deferred to Phase 5 integration testing
- Persistence across runtime restarts — only meaningful with a real backend, deferred

---

## Context

### Current State

| Component | Status |
|-----------|--------|
| `IPersistentStorage` interface | ✅ Complete — all 5 methods defined |
| `InMemoryStorage` backend | ✅ Complete — all 5 methods implemented |
| `WriteDriver` | ⚠️ Missing `ZohExpr` type check |
| `ReadDriver` | ✅ Complete — handles `[required]`, `[scope]`, `default:`, type-safe set |
| `EraseDriver` | ❌ Missing |
| `PurgeDriver` | ❌ Missing |
| `VerbRegistry` registration | ⚠️ Only Write and Read registered |
| Test coverage | ⚠️ 5 tests — no erase/purge/type-rejection/named-store tests |

### Key Files

| File | Purpose | Changes Needed |
|------|---------|----------------|
| `c#/src/Zoh.Runtime/Verbs/Store/EraseDriver.cs` | [NEW] | Implement `/erase` verb driver |
| `c#/src/Zoh.Runtime/Verbs/Store/PurgeDriver.cs` | [NEW] | Implement `/purge` verb driver |
| `c#/src/Zoh.Runtime/Verbs/Store/WriteDriver.cs` | Write verb driver | Add `ZohExpr` to type rejection |
| `c#/src/Zoh.Runtime/Verbs/VerbRegistry.cs` | Verb registration | Register `EraseDriver` and `PurgeDriver` |
| `c#/tests/Zoh.Tests/Verbs/Store/StoreVerbTests.cs` | Storage tests | Add comprehensive test cases |

### Dependencies

- **Requires:** None — all infrastructure (`IPersistentStorage`, `InMemoryStorage`, `IVerbDriver`, `VerbResult`, `TestExecutionContext`) already exists
- **Blocks:** None directly; future serialization work builds on these drivers

### Constraints

- Must follow the `impl/11_storage.md` pseudocode precisely (the spec is more authoritative than existing code)
- Driver pattern must match existing conventions: `IVerbDriver` with `Namespace => "store"`, `Name => "verbname"`
- Use `ValueResolver.Resolve()` for parameter resolution, consistent with `WriteDriver`/`ReadDriver`
- Type rejection must cover all three non-serializable types: `ZohVerb`, `ZohChannel`, `ZohExpr`

---

## Implementation

### Overview

Three-step implementation: (1) fix WriteDriver, (2) add EraseDriver and PurgeDriver, (3) register new drivers and add tests.

---

### Step 1: Fix WriteDriver — Add ZohExpr Type Check

**Objective:** Align `WriteDriver` with spec — reject expression values alongside verb and channel.

**Files:**
- `c#/src/Zoh.Runtime/Verbs/Store/WriteDriver.cs`

**Changes:**

```csharp
// Before (line 48):
if (value is ZohVerb || value is ZohChannel)

// After:
if (value is ZohVerb || value is ZohChannel || value is ZohExpr)
```

Add the missing `using` if needed:
```csharp
using Zoh.Runtime.Types; // Already imported — ZohExpr is in this namespace
```

**Rationale:** `impl/11_storage.md` line 89: `if value.getType() in ["verb", "channel", "expression"]` — the code only checks two of three.

**Verification:** New test `Write_ExpressionType_ReturnsFatal` (see Step 3).

---

### Step 2: Implement EraseDriver and PurgeDriver

**Objective:** Create the two missing verb drivers following the pseudocode in `impl/11_storage.md`.

#### 2a: EraseDriver

**Files:**
- `c#/src/Zoh.Runtime/Verbs/Store/EraseDriver.cs` [NEW]

**Implementation:**

```csharp
using System.Linq;
using Zoh.Runtime.Execution;
using Zoh.Runtime.Parsing.Ast;
using Zoh.Runtime.Types;
using Zoh.Runtime.Diagnostics;

namespace Zoh.Runtime.Verbs.Store;

public class EraseDriver : IVerbDriver
{
    public string Namespace => "store";
    public string Name => "erase";

    public VerbResult Execute(IExecutionContext context, VerbCallAst verb)
    {
        // /erase store:"name"?, *var;
        string? storeName = null;
        if (verb.NamedParams.TryGetValue("store", out var storeValAst))
        {
            var storeVal = ValueResolver.Resolve(storeValAst, context);
            if (storeVal is ZohStr s) storeName = s.Value;
        }

        var refs = verb.UnnamedParams.OfType<ValueAst.Reference>().ToList();
        if (refs.Count == 0)
        {
            return VerbResult.Fatal(new Diagnostic(
                DiagnosticSeverity.Fatal, "parameter_not_found",
                "Erase requires at least one reference parameter", verb.Start));
        }

        foreach (var varRef in refs)
        {
            if (!context.Storage.Exists(storeName, varRef.Name))
            {
                // Spec: return ok with info diagnostic for not-found
                return VerbResult.WithDiagnostics(ZohValue.Nothing, new[]
                {
                    new Diagnostic(DiagnosticSeverity.Info, "not_found",
                        $"Variable not in storage: {varRef.Name}", verb.Start)
                });
            }
            context.Storage.Erase(storeName, varRef.Name);
        }

        return VerbResult.Ok();
    }
}
```

**Rationale:** Follows `impl/11_storage.md` EraseDriver pseudocode exactly. The spec shows single `*var` param but we loop over refs for consistency with Write/Read (which accept multiple). Info diagnostic on not-found matches the spec.

> [!NOTE]
> The spec pseudocode shows a single `*var` param (`call.params[0]`), but the pattern used throughout the codebase for store verbs accepts multiple refs via `OfType<Reference>()`. We follow the multi-ref pattern for consistency, while the single-ref form still works as a special case.

#### 2b: PurgeDriver

**Files:**
- `c#/src/Zoh.Runtime/Verbs/Store/PurgeDriver.cs` [NEW]

**Implementation:**

```csharp
using Zoh.Runtime.Execution;
using Zoh.Runtime.Parsing.Ast;
using Zoh.Runtime.Types;
using Zoh.Runtime.Diagnostics;

namespace Zoh.Runtime.Verbs.Store;

public class PurgeDriver : IVerbDriver
{
    public string Namespace => "store";
    public string Name => "purge";

    public VerbResult Execute(IExecutionContext context, VerbCallAst verb)
    {
        // /purge store:"name"?;
        string? storeName = null;
        if (verb.NamedParams.TryGetValue("store", out var storeValAst))
        {
            var storeVal = ValueResolver.Resolve(storeValAst, context);
            if (storeVal is ZohStr s) storeName = s.Value;
        }

        context.Storage.Purge(storeName);
        return VerbResult.Ok();
    }
}
```

**Rationale:** Simplest driver — just delegates to storage. Matches `impl/11_storage.md` PurgeDriver pseudocode.

---

### Step 3: Register Drivers and Add Tests

**Objective:** Register new drivers in `VerbRegistry` and add comprehensive test coverage.

#### 3a: VerbRegistry Registration

**Files:**
- `c#/src/Zoh.Runtime/Verbs/VerbRegistry.cs`

**Changes:**

```csharp
// After line 154 (existing Store.ReadDriver registration):
Register(new Store.ReadDriver());

// Add:
Register(new Store.EraseDriver());
Register(new Store.PurgeDriver());
```

#### 3b: Comprehensive Tests

**Files:**
- `c#/tests/Zoh.Tests/Verbs/Store/StoreVerbTests.cs`

New test cases to add (maps to `impl/11_storage.md` testing checklist):

| Category | Test Method | Spec Checklist Item |
|----------|-------------|---------------------|
| **Basic Ops** | `Erase_ExistingVariable_RemovesFromStorage` | Erase single variable |
| **Basic Ops** | `Erase_NonExistentVariable_ReturnsInfoDiagnostic` | Erase not-found handling |
| **Basic Ops** | `Purge_ClearsEntireStore` | Purge entire store |
| **Basic Ops** | `Write_Read_NamedStore` | Write/read from named store |
| **Basic Ops** | `Purge_NamedStore_OnlyAffectsTargetStore` | Purge isolation |
| **Type Handling** | `Write_AllSerializableTypes_Succeeds` | All serializable types |
| **Type Handling** | `Write_VerbType_ReturnsFatal` | Non-serializable type error |
| **Type Handling** | `Write_ChannelType_ReturnsFatal` | Non-serializable type error |
| **Type Handling** | `Write_ExpressionType_ReturnsFatal` | Non-serializable type error |
| **Repeating** | `Erase_NoRefs_ReturnsFatal` | No reference params error |
| **Attributes** | `Read_RequiredWithScopedStore_ReturnsError` | `[required]` on named store |

**Verification:** `dotnet test` in `c#/` — all tests pass.

---

## Verification Plan

### Automated Checks

- [ ] `dotnet build` in `c#/` — zero errors, zero warnings from new code
- [ ] `dotnet test` in `c#/` — all existing 515+ tests pass, and ~11 new tests pass

### Acceptance Criteria Validation

| Criterion | How to Verify | Expected Result |
|-----------|---------------|-----------------|
| EraseDriver works | `Erase_ExistingVariable_RemovesFromStorage` test | Storage no longer contains the variable |
| PurgeDriver works | `Purge_ClearsEntireStore` test | Storage empty after purge |
| Expression type rejected | `Write_ExpressionType_ReturnsFatal` test | Fatal with `invalid_type` code |
| Drivers registered | Build succeeds + tests run verbs by name | No registration errors |
| Named store isolation | `Purge_NamedStore_OnlyAffectsTargetStore` test | Only target store cleared |

---

## Rollback Plan

If implementation fails or causes issues:

1. Revert the two new files (`EraseDriver.cs`, `PurgeDriver.cs`)
2. Revert the one-line change in `WriteDriver.cs`
3. Revert the two registration lines in `VerbRegistry.cs`
4. Revert test additions

All changes are additive — no existing functionality is modified except the one `WriteDriver` type check extension.

---

## Notes

### Assumptions

- `InMemoryStorage` is the only backend needed for this phase (per navigation roadmap)
- `VerbResult.Ok(ZohValue, Diagnostic)` overload exists for returning diagnostics with success (used in EraseDriver for not-found info)
- The `ZohExpr` type is in the `Zoh.Runtime.Types` namespace (confirmed: `ZohExpr.cs`)

### Risks

- **VerbResult.Ok with diagnostics:** If no overload exists, we may need to use `VerbResult.Ok().WithDiagnostics()` — need to verify the actual API during implementation
- **EraseDriver multi-ref semantics:** Spec shows single ref, we implement multi-ref loop. Risk: if one ref is not found, should we continue or return early? Spec implies return early with ok + diagnostic. We follow that: first not-found causes early return.

### Open Questions

- None — all details are clear from the spec and existing patterns.
