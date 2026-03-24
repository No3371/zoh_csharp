# Replace `invalid_arg` diagnostic code with `invalid_type` / `invalid_params`

> **Status:** Ready
> **Created:** 2026-03-20
> **Author:** agent
> **Source:** Direct request
> **Related Projex:** 2603201730-type-mismatch-scope-creep-memo.md

---

## Summary

Replace all usages of the unspecified `invalid_arg` diagnostic code with `invalid_type`, and `invalid_args` with `invalid_params`. Both replacement codes are well-established in the codebase.

**Scope:** 7 driver files in `csharp/src/Zoh.Runtime/Verbs/`, 13 diagnostic sites
**Estimated Changes:** 7 files, 13 lines

---

## Objective

### Problem / Gap / Need

`invalid_arg` and `invalid_args` are ad-hoc diagnostic codes with no spec definition. The codebase already has `invalid_type` (35+ usages) for type-check failures and `invalid_params` (9 usages) for structural param issues. The `invalid_arg` usages are all type checks and should use `invalid_type`.

### Success Criteria

- [ ] Zero occurrences of `"invalid_arg"` or `"invalid_args"` in `csharp/src/`
- [ ] All replacements use the correct code (`invalid_type` for type checks, `invalid_params` for structure checks)
- [ ] RollDriver severity corrected from `Error` to `Fatal` for consistency
- [ ] All tests pass

### Out of Scope

- `type_mismatch` consolidation (tracked separately in memo)
- Adding diagnostic codes to the spec
- Changing diagnostic messages

---

## Context

### Current State

All `invalid_arg` usages follow the same pattern — a resolved param value is checked against an expected ZOH type, and a fatal diagnostic is emitted if wrong. This is identical semantics to `invalid_type`, which is used for the same purpose in 35+ other sites.

RollDriver uses `invalid_args` (plural) with `DiagnosticSeverity.Error` — both inconsistent. It checks param count (not type), so `invalid_params` is correct. All other navigation verb type checks use `Fatal`.

### Key Files

| File | Role | Change Summary |
|------|------|----------------|
| `src/Zoh.Runtime/Verbs/Flow/JumpDriver.cs` | Jump navigation | `invalid_arg` → `invalid_type` (3 sites) |
| `src/Zoh.Runtime/Verbs/Flow/ForkDriver.cs` | Fork navigation | `invalid_arg` → `invalid_type` (2 sites) |
| `src/Zoh.Runtime/Verbs/Flow/CallDriver.cs` | Call navigation | `invalid_arg` → `invalid_type` (3 sites) |
| `src/Zoh.Runtime/Verbs/Flow/SleepDriver.cs` | Sleep/pause | `invalid_arg` → `invalid_type` (1 site) |
| `src/Zoh.Runtime/Verbs/Signals/WaitDriver.cs` | Wait for signal | `invalid_arg` → `invalid_type` (1 site) |
| `src/Zoh.Runtime/Verbs/Signals/SignalDriver.cs` | Emit signal | `invalid_arg` → `invalid_type` (1 site) |
| `src/Zoh.Runtime/Verbs/Core/RollDriver.cs` | Weighted random | `invalid_args` → `invalid_params`, severity `Error` → `Fatal` (1 site) |

### Assumptions

- No tests assert on the literal string `"invalid_arg"` — verified below

---

## Implementation

### Overview

Pure string replacement of diagnostic codes, plus one severity fix. No behavioral changes.

### Step 1: Replace `invalid_arg` → `invalid_type` in flow/signal drivers

**Objective:** Align 11 type-check diagnostics with the established `invalid_type` code
**Confidence:** High
**Depends on:** None

**Files:**
- `src/Zoh.Runtime/Verbs/Flow/JumpDriver.cs` (lines 34, 42, 46)
- `src/Zoh.Runtime/Verbs/Flow/ForkDriver.cs` (lines 34, 41, 45)
- `src/Zoh.Runtime/Verbs/Flow/CallDriver.cs` (lines 48, 92, 104)
- `src/Zoh.Runtime/Verbs/Flow/SleepDriver.cs` (line 29)
- `src/Zoh.Runtime/Verbs/Signals/WaitDriver.cs` (line 28)
- `src/Zoh.Runtime/Verbs/Signals/SignalDriver.cs` (line 27)

**Changes:**

In each file, replace `"invalid_arg"` with `"invalid_type"`. Messages unchanged.

```csharp
// Before:
new Diagnostic(DiagnosticSeverity.Fatal, "invalid_arg", "Jump label must be a string.", call.Start)

// After:
new Diagnostic(DiagnosticSeverity.Fatal, "invalid_type", "Jump label must be a string.", call.Start)
```

**Rationale:** These are all type-check failures on resolved values — exactly what `invalid_type` means throughout the codebase.

**Verification:** `grep -r "invalid_arg" csharp/src/ --include="*.cs"` returns only RollDriver's `invalid_args`

### Step 2: Replace `invalid_args` → `invalid_params` in RollDriver + fix severity

**Objective:** Align RollDriver's param-count check with established `invalid_params` code and `Fatal` severity
**Confidence:** High
**Depends on:** None

**Files:**
- `src/Zoh.Runtime/Verbs/Core/RollDriver.cs` (line 51)

**Changes:**

```csharp
// Before:
return DriverResult.Complete.Fatal(new Diagnostics.Diagnostic(Diagnostics.DiagnosticSeverity.Error, "invalid_args", "Weighted roll requires pairs of (value, weight)", verb.Start));

// After:
return DriverResult.Complete.Fatal(new Diagnostics.Diagnostic(Diagnostics.DiagnosticSeverity.Fatal, "invalid_params", "Weighted roll requires pairs of (value, weight)", verb.Start));
```

**Rationale:** This checks param structure (even count), not type — matches `invalid_params` semantics. Severity `Error` is inconsistent with all other navigation/core verb diagnostics that use `Fatal` for unusable input.

**Verification:** `grep -r "invalid_args\|invalid_arg" csharp/src/ --include="*.cs"` returns zero results

---

## Verification Plan

### Automated Checks
- [ ] `dotnet build` succeeds
- [ ] `dotnet test` — all tests pass
- [ ] `grep -r '"invalid_arg"' csharp/src/ --include="*.cs"` — zero results
- [ ] `grep -r '"invalid_args"' csharp/src/ --include="*.cs"` — zero results

### Acceptance Criteria Validation

| Criterion | How to Verify | Expected Result |
|-----------|---------------|-----------------|
| Zero `invalid_arg` in src | grep | No matches |
| Correct replacement codes | Code review | Type checks → `invalid_type`, structure → `invalid_params` |
| RollDriver severity fixed | Read line 51 | `DiagnosticSeverity.Fatal` |
| Tests pass | `dotnet test` | All green |

---

## Rollback Plan

Revert the 7 files — pure string changes, no structural modifications.

---

## Notes

### Risks
- None — mechanical string replacement with no behavioral impact beyond diagnostic code values
