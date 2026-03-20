# Diagnostic Code: Standardize to `invalid_params`

> **Status:** Complete
> **Created:** 2026-03-20
> **Completed:** 2026-03-20
> **Author:** Agent
> **Source:** Direct request
> **Related Projex:** 20260316-spec-catchup-followup.md, 20260316-presentation-verb-diagnostics-alignment-plan.md, 202603201807-diagnostic-code-invalid-params-plan.md (sibling — impl spec)
> **Walkthrough:** 202603201757-diagnostic-code-invalid-params-walkthrough.md

---

## Summary

Several drivers use ad hoc codes (`arg_count`, `no_choices`) that are not consistent with the rest of the codebase's emerging vocabulary. This plan replaces both with a single `invalid_params` code, making error code conventions uniform across all drivers.

**Scope:** C# runtime drivers and their tests — no spec or parser changes.
**Estimated Changes:** 9 code sites across 8 source files; 2 test files updated + new tests added for previously untested drivers.

---

## Objective

### Problem / Gap / Need

Two diagnostic codes are inconsistent with emerging norms:

| Code | Where Used | Issue |
|------|-----------|-------|
| `arg_count` | JumpDriver (×2), ForkDriver, CallDriver (×2), SleepDriver, WaitDriver, SignalDriver | Not in impl spec. The impl spec uses `parameter_not_found` for missing args; this is a similar category but distinct unsanctioned spelling. |
| `no_choices` | ChooseDriver, ChooseFromDriver | Spec-defined in `impl/10_std_verbs.md`, but overly choice-domain-specific. |

Both are replaced with `invalid_params` — a clear, general-purpose code for "verb cannot proceed because the supplied parameters are invalid or insufficient".

Bonus fix included: `ChooseFromDriver` uses `"type_error"` in one location where the impl spec (`10_std_verbs.md` line 179) calls for `"invalid_type"`.

### Success Criteria

- [ ] `arg_count` no longer appears anywhere in the C# codebase
- [ ] `no_choices` no longer appears anywhere in the C# codebase
- [ ] `"type_error"` in `ChooseFromDriver` replaced with `"invalid_type"`
- [ ] All existing tests that asserted `arg_count` or `no_choices` updated to assert `invalid_params`
- [ ] New tests added for all `arg_count` drivers (Jump, Fork, Call, Sleep, Wait, Signal) asserting `invalid_params`
- [ ] Full test suite passes: `dotnet test` in `csharp/`

### Out of Scope

- Changing `parameter_not_found` — separate canonical code used elsewhere; not consolidated here
- Spec changes to `impl/10_std_verbs.md` documenting the removed `no_choices` — tracked in followup
- Any driver not listed in the Key Files table below

---

## Context

### Current State

**`arg_count` usage (all in `src/Zoh.Runtime/Verbs/`):**

| File | Line | Message |
|------|------|---------|
| `Flow/JumpDriver.cs:50` | Jump requires 1 or 2 arguments |
| `Flow/JumpDriver.cs:44` | Call requires a label after a null story |
| `Flow/ForkDriver.cs:49` | Fork requires 1 or 2 arguments |
| `Flow/CallDriver.cs:32` | Call requires at least 1 argument |
| `Flow/CallDriver.cs:44` | Call requires a label after a null story |
| `Flow/SleepDriver.cs:21` | Sleep requires 1 argument |
| `Signals/WaitDriver.cs:22` | Wait requires at least 1 argument |
| `Signals/SignalDriver.cs:21` | Signal requires at least 1 argument |

**`no_choices` usage:**

| File | Line | Message |
|------|------|---------|
| `Standard/Presentation/ChooseDriver.cs:102` | No visible choices |
| `Standard/Presentation/ChooseFromDriver.cs:100` | ChooseFrom has no visible choices and no timeout |

**`type_error` usage (bonus fix):**

| File | Line | Should be |
|------|------|-----------|
| `Standard/Presentation/ChooseFromDriver.cs:93` | `invalid_type` per impl spec |

**Existing tests asserting these codes:**

| Test File | Assertion |
|-----------|-----------|
| `tests/Zoh.Tests/Verbs/Standard/Presentation/ChooseDriverTests.cs:197` | `d.Code == "no_choices"` |
| `tests/Zoh.Tests/Verbs/Standard/Presentation/ChooseFromDriverTests.cs:185` | `d.Code == "no_choices"` |

No tests currently assert `arg_count` — it is completely untested. New tests will be added.

### Key Files

| File | Role | Change Summary |
|------|------|----------------|
| `src/Zoh.Runtime/Verbs/Flow/JumpDriver.cs` | Navigation | `arg_count` → `invalid_params` (×2) |
| `src/Zoh.Runtime/Verbs/Flow/ForkDriver.cs` | Navigation | `arg_count` → `invalid_params` |
| `src/Zoh.Runtime/Verbs/Flow/CallDriver.cs` | Navigation | `arg_count` → `invalid_params` (×2) |
| `src/Zoh.Runtime/Verbs/Flow/SleepDriver.cs` | Flow | `arg_count` → `invalid_params` |
| `src/Zoh.Runtime/Verbs/Signals/WaitDriver.cs` | Signals | `arg_count` → `invalid_params` |
| `src/Zoh.Runtime/Verbs/Signals/SignalDriver.cs` | Signals | `arg_count` → `invalid_params` |
| `src/Zoh.Runtime/Verbs/Standard/Presentation/ChooseDriver.cs` | Presentation | `no_choices` → `invalid_params` |
| `src/Zoh.Runtime/Verbs/Standard/Presentation/ChooseFromDriver.cs` | Presentation | `no_choices` → `invalid_params`; `type_error` → `invalid_type` |
| `tests/Zoh.Tests/Verbs/Standard/Presentation/ChooseDriverTests.cs` | Tests | Update assertion |
| `tests/Zoh.Tests/Verbs/Standard/Presentation/ChooseFromDriverTests.cs` | Tests | Update assertion |
| `tests/Zoh.Tests/Verbs/Flow/FlowErrorTests.cs` | Tests | Add `invalid_params` tests for Jump, Fork, Call |
| `tests/Zoh.Tests/Verbs/Flow/SleepTests.cs` | Tests | Add missing-arg test |
| `tests/Zoh.Tests/Verbs/Flow/ConcurrencyTests.cs` (or new file) | Tests | Add invalid_params tests for Wait, Signal |

### Dependencies

- **Requires:** None — all changes are local to C# runtime
- **Blocks:** Nothing

### Assumptions

- `ChooseFromDriver` with no choices does **not** currently occur at the implementation level (the `no_choices` path for `ChooseFromDriver` requires that the list is empty after filtering visible entries — this is separate from the empty-list detection in `ChooseDriver`)
- Navigation drivers (Jump, Fork, Call) already have some coverage in `NavigationTests.cs` and `ConcurrencyTests.cs`; the new tests cover specifically the missing-arg fatal path

### Impact Analysis

- **Direct:** 8 source driver files, 2 existing test files, 3+ test files extended
- **Adjacent:** Any host code matching against diagnostic codes from these drivers must be updated — but there is no production host code in this repo that pattern-matches these codes
- **Downstream:** External hosts using string `"arg_count"` or `"no_choices"` in diagnostic filtering will need to update — this is a breaking change to the diagnostic API surface

---

## Implementation

### Overview

A mechanical text replacement across 8 source files, then updating test assertions and adding new tests for currently-untested paths. All changes are in one commit.

---

### Step 1: Replace `arg_count` → `invalid_params` in Flow Drivers

**Objective:** Eliminate `arg_count` from navigation and flow drivers.
**Confidence:** High
**Depends on:** None

**Files:**
- `src/Zoh.Runtime/Verbs/Flow/JumpDriver.cs`
- `src/Zoh.Runtime/Verbs/Flow/ForkDriver.cs`
- `src/Zoh.Runtime/Verbs/Flow/CallDriver.cs`
- `src/Zoh.Runtime/Verbs/Flow/SleepDriver.cs`

**Changes:**

```csharp
// Before (example — same pattern in all four files):
return DriverResult.Complete.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "arg_count", "...", call.Start));

// After:
return DriverResult.Complete.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "invalid_params", "...", call.Start));
```

**Rationale:** `arg_count` is not a recognized impl-spec code; `invalid_params` is consistent with how the team wants to express "verb cannot run with the parameters given".

**Verification:** `grep -r "arg_count" src/` returns no matches in these four files after edit.

**If this fails:** Revert file changes; no side effects.

---

### Step 2: Replace `arg_count` → `invalid_params` in Signal Drivers

**Objective:** Eliminate `arg_count` from signal drivers.
**Confidence:** High
**Depends on:** None (can run in parallel with Step 1)

**Files:**
- `src/Zoh.Runtime/Verbs/Signals/WaitDriver.cs`
- `src/Zoh.Runtime/Verbs/Signals/SignalDriver.cs`

**Changes:** Same string replacement pattern as Step 1.

**Verification:** `grep -r "arg_count" src/Signals/` returns no matches.

---

### Step 3: Replace `no_choices` → `invalid_params` in Presentation Drivers

**Objective:** Eliminate `no_choices`; also fix `type_error` → `invalid_type` in `ChooseFromDriver`.
**Confidence:** High
**Depends on:** None

**Files:**
- `src/Zoh.Runtime/Verbs/Standard/Presentation/ChooseDriver.cs`
- `src/Zoh.Runtime/Verbs/Standard/Presentation/ChooseFromDriver.cs`

**Changes:**

```csharp
// ChooseDriver.cs line ~102 — Before:
new Diagnostic(DiagnosticSeverity.Warning, "no_choices", "No visible choices", call.Start)
// After:
new Diagnostic(DiagnosticSeverity.Warning, "invalid_params", "No visible choices", call.Start)

// ChooseFromDriver.cs line ~100 — Before:
new Diagnostic(DiagnosticSeverity.Warning, "no_choices", "ChooseFrom has no visible choices and no timeout.", call.Start)
// After:
new Diagnostic(DiagnosticSeverity.Warning, "invalid_params", "ChooseFrom has no visible choices and no timeout.", call.Start)

// ChooseFromDriver.cs line ~93 — Before (bonus fix):
return DriverResult.Complete.Fatal(new Diagnostic(DiagnosticSeverity.Error, "type_error", "chooseFrom requires a list as its first argument.", call.Start));
// After:
return DriverResult.Complete.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "invalid_type", "chooseFrom requires a list as its first argument.", call.Start));
```

Note: The severity level on `ChooseFromDriver.cs:93` also changes from `Error` to `Fatal` — the original code uses `DiagnosticSeverity.Error` but calls `.Fatal(...)`, which is contradictory. The `Fatal` wrapper takes precedence; aligning to `Fatal` severity is correct.

**Verification:** `grep -r "no_choices\|type_error" src/` returns no matches after edit.

---

### Step 4: Update Presentation Tests

**Objective:** Update the two existing assertions from `"no_choices"` to `"invalid_params"`.
**Confidence:** High
**Depends on:** Step 3

**Files:**
- `tests/Zoh.Tests/Verbs/Standard/Presentation/ChooseDriverTests.cs` (line 197)
- `tests/Zoh.Tests/Verbs/Standard/Presentation/ChooseFromDriverTests.cs` (line 185)

**Changes:**

```csharp
// Before:
Assert.Contains(internalCtx.LastDiagnostics, d => d.Severity == DiagnosticSeverity.Warning && d.Code == "no_choices");

// After:
Assert.Contains(internalCtx.LastDiagnostics, d => d.Severity == DiagnosticSeverity.Warning && d.Code == "invalid_params");
```

**Verification:** `dotnet test --filter "FullyQualifiedName~Presentation"` passes.

---

### Step 5: Add Tests for `arg_count` Drivers (now `invalid_params`)

**Objective:** Cover the missing-arg fatal path for all 6 drivers that previously used `arg_count`.
**Confidence:** High
**Depends on:** Steps 1–2

**Files:**
- `tests/Zoh.Tests/Verbs/Flow/FlowErrorTests.cs` — add tests for Jump, Fork, Call (register drivers in constructor or separate test class as needed)
- `tests/Zoh.Tests/Verbs/Flow/SleepTests.cs` — add missing-arg test
- `tests/Zoh.Tests/Verbs/Flow/ConcurrencyTests.cs` — add missing-arg tests for Wait and Signal (or new `SignalErrorTests.cs`)

**New test pattern (example for SleepDriver):**

```csharp
[Fact]
public void Sleep_MissingArgument_ReturnsFatalWithInvalidParams()
{
    var driver = new SleepDriver();
    var call = new VerbCallAst(null, "sleep", false, ImmutableArray<AttributeAst>.Empty,
        ImmutableDictionary<string, ValueAst>.Empty,
        ImmutableArray<ValueAst>.Empty, // no args
        new TextPosition(0, 0, 0));
    var ctx = CreateContext();
    var result = driver.Execute(ctx, call);
    Assert.True(result.IsFatal);
    Assert.Contains(result.DiagnosticsOrEmpty, d => d.Code == "invalid_params");
}
```

Similar tests for: `JumpDriver` (0 args), `ForkDriver` (0 args), `CallDriver` (0 args), `WaitDriver` (0 args), `SignalDriver` (0 args).

**Verification:** `dotnet test --filter "MissingArgument"` passes for all new tests.

---

## Verification Plan

> Per-step verification above confirms each change in isolation. This section confirms they work together.

### Automated Checks

- [ ] `cd csharp && dotnet build` — no errors or new warnings
- [ ] `cd csharp && dotnet test` — all tests pass
- [ ] `grep -r "arg_count\|no_choices\|type_error" csharp/src/` — no matches

### Manual Verification

*(None required — all outcomes are verified by automated tests.)*

### Acceptance Criteria Validation

| Criterion | How to Verify | Expected Result |
|-----------|---------------|-----------------|
| `arg_count` eliminated | `grep -r "arg_count" csharp/src/` | No output |
| `no_choices` eliminated | `grep -r "no_choices" csharp/src/` | No output |
| `type_error` fixed | `grep -r "type_error" csharp/src/` | No output |
| Tests pass | `cd csharp && dotnet test` | All green |
| New tests cover previously untested paths | `dotnet test --filter "MissingArgument\|InvalidParams"` | ≥6 new passing tests |

---

## Rollback Plan

1. `git revert` the commit, or restore each file from `git show HEAD:<path>`
2. No schema or external artifact migrations required

---

## Notes

### Risks

- **Breaking API change for external hosts:** Any host using `"arg_count"` or `"no_choices"` as a string match will silently stop matching. Mitigation: this repo has no production host code; the change is safe in isolation.
- **`ChooseFromDriver` severity fix (Step 3):** Changing from `Error` to `Fatal` severity is technically more impactful than a code rename. The old code was self-contradictory (called `.Fatal()` with `DiagnosticSeverity.Error`), so this aligns intent with implementation.

### Open Questions

- None — all changes are deterministic.
