# Walkthrough: Diagnostic Code Standardize to `invalid_params`

> **Execution Date:** 2026-03-20
> **Completed By:** Agent
> **Source Plan:** 202603201757-diagnostic-code-invalid-params-plan.md
> **Duration:** ~10 minutes
> **Result:** Success

---

## Summary

All nine `arg_count`/`no_choices`/`type_error` diagnostic code occurrences across 8 source driver files were replaced with the standardized codes (`invalid_params` or `invalid_type`). Two existing test assertions were updated and 6 new tests were added to cover the previously untested `invalid_params` paths. All 682 tests pass.

---

## Objectives Completion

| Objective | Status | Notes |
|-----------|--------|-------|
| Eliminate `arg_count` from C# source | Complete | 7 occurrences removed across 6 drivers |
| Eliminate `no_choices` from C# source | Complete | 2 occurrences removed |
| Fix `type_error` → `invalid_type` in `ChooseFromDriver` | Complete | Also fixed severity `Error` → `Fatal` |
| Update existing test assertions | Complete | 2 tests updated |
| Add new tests for previously untested arg-count paths | Complete | 6 new tests added |
| Full test suite passes | Complete | 682 passed, 0 failed |

---

## Execution Detail

### Steps 1–2: Replace `arg_count` → `invalid_params` in Flow and Signal Drivers

**Planned:** Replace all `"arg_count"` literals in 6 drivers (Jump, Fork, Call, Sleep, Wait, Signal).

**Actual:** All 9 occurrences (7 in flow drivers, 2 in signal drivers — CallDriver has 2) replaced. Exact line-level replacements, no other logic touched.

**Deviation:** None.

**Files Changed:**
| File | Change | Details |
|------|--------|---------|
| `src/Zoh.Runtime/Verbs/Flow/JumpDriver.cs` | Modified | Line 50: `arg_count` → `invalid_params` |
| `src/Zoh.Runtime/Verbs/Flow/ForkDriver.cs` | Modified | Line 49: `arg_count` → `invalid_params` |
| `src/Zoh.Runtime/Verbs/Flow/CallDriver.cs` | Modified | Lines 32, 44: `arg_count` → `invalid_params` (×2) |
| `src/Zoh.Runtime/Verbs/Flow/SleepDriver.cs` | Modified | Line 21: `arg_count` → `invalid_params` |
| `src/Zoh.Runtime/Verbs/Signals/WaitDriver.cs` | Modified | Line 22: `arg_count` → `invalid_params` |
| `src/Zoh.Runtime/Verbs/Signals/SignalDriver.cs` | Modified | Line 21: `arg_count` → `invalid_params` |

**Verification:** `Get-ChildItem -Recurse -Filter "*.cs" src | Select-String -Pattern "arg_count"` → 0 matches.

---

### Step 3: Replace `no_choices` → `invalid_params` and fix `type_error` in Presentation Drivers

**Planned:** Replace `no_choices` in 2 drivers; fix `type_error` → `invalid_type` in `ChooseFromDriver`.

**Actual:** All replacements applied. Additionally fixed severity from `DiagnosticSeverity.Error` to `DiagnosticSeverity.Fatal` on the `ChooseFromDriver` type-check path (the old code was self-contradictory: called `.Fatal()` wrapper but passed `Error` severity).

**Deviation:** None (severity fix was called out in the plan).

**Files Changed:**
| File | Change | Details |
|------|--------|---------|
| `src/Zoh.Runtime/Verbs/Standard/Presentation/ChooseDriver.cs` | Modified | Line 102: `no_choices` → `invalid_params` |
| `src/Zoh.Runtime/Verbs/Standard/Presentation/ChooseFromDriver.cs` | Modified | Line 93: `type_error` → `invalid_type`, severity `Error`→`Fatal`; Line 100: `no_choices` → `invalid_params` |

**Verification:** `Get-ChildItem -Recurse -Filter "*.cs" src | Select-String -Pattern "no_choices|type_error"` → 0 matches.

---

### Step 4: Update Presentation Test Assertions

**Planned:** Update `d.Code == "no_choices"` → `d.Code == "invalid_params"` in 2 test files.

**Actual:** Updated both assertions as planned.

**Files Changed:**
| File | Change | Details |
|------|--------|---------|
| `tests/Zoh.Tests/Verbs/Standard/Presentation/ChooseDriverTests.cs` | Modified | Line 197: `no_choices` → `invalid_params` |
| `tests/Zoh.Tests/Verbs/Standard/Presentation/ChooseFromDriverTests.cs` | Modified | Line 185: `no_choices` → `invalid_params` |

---

### Step 5: Add New `invalid_params` Tests

**Planned:** Add tests for Jump, Fork, Call in `FlowErrorTests.cs`; Sleep in `SleepTests.cs`; Wait/Signal in `ConcurrencyTests.cs` (or new file).

**Actual:** Jump/Fork/Call tests placed in `ConcurrencyTests.cs` (not `FlowErrorTests.cs`). Wait/Signal tests placed in `SleepTests.cs` (not `ConcurrencyTests.cs`).

**Deviation:** Test file placement adjusted (see Deviations). All 6 tests cover the same paths the plan intended.

**Files Changed:**
| File | Change | Details |
|------|--------|---------|
| `tests/Zoh.Tests/Verbs/Flow/ConcurrencyTests.cs` | Modified | Added `Jump_MissingArgument`, `Fork_MissingArgument`, `Call_MissingArgument` |
| `tests/Zoh.Tests/Verbs/Flow/SleepTests.cs` | Modified | Added `Sleep_MissingArgument`, `Wait_MissingArgument`, `Signal_MissingArgument`; added `using Zoh.Runtime.Verbs.Signals` |

**Verification:** `dotnet test` → 682 passed, 0 failed.

---

## Complete Change Log

> Derived from `git diff --stat main..HEAD`

### Files Created
| File | Purpose | In Plan? |
|------|---------|----------|
| `projex/202603201757-diagnostic-code-invalid-params-log.md` | Execution log | Yes (generated) |

### Files Modified
| File | Changes | In Plan? |
|------|---------|----------|
| `src/Zoh.Runtime/Verbs/Flow/JumpDriver.cs` | `arg_count` → `invalid_params` | Yes |
| `src/Zoh.Runtime/Verbs/Flow/ForkDriver.cs` | `arg_count` → `invalid_params` | Yes |
| `src/Zoh.Runtime/Verbs/Flow/CallDriver.cs` | `arg_count` → `invalid_params` (×2) | Yes |
| `src/Zoh.Runtime/Verbs/Flow/SleepDriver.cs` | `arg_count` → `invalid_params` | Yes |
| `src/Zoh.Runtime/Verbs/Signals/WaitDriver.cs` | `arg_count` → `invalid_params` | Yes |
| `src/Zoh.Runtime/Verbs/Signals/SignalDriver.cs` | `arg_count` → `invalid_params` | Yes |
| `src/Zoh.Runtime/Verbs/Standard/Presentation/ChooseDriver.cs` | `no_choices` → `invalid_params` | Yes |
| `src/Zoh.Runtime/Verbs/Standard/Presentation/ChooseFromDriver.cs` | `no_choices` → `invalid_params`; `type_error` → `invalid_type`; severity fix | Yes |
| `tests/.../ChooseDriverTests.cs` | Assertion `no_choices` → `invalid_params` | Yes |
| `tests/.../ChooseFromDriverTests.cs` | Assertion `no_choices` → `invalid_params` | Yes |
| `tests/Zoh.Tests/Verbs/Flow/ConcurrencyTests.cs` | 3 new tests (Jump/Fork/Call missing-arg) | Yes (file different from plan) |
| `tests/Zoh.Tests/Verbs/Flow/SleepTests.cs` | 3 new tests (Sleep/Wait/Signal missing-arg); new import | Yes (file different from plan) |
| `projex/202603201757-diagnostic-code-invalid-params-plan.md` | Status `Draft` → `In Progress` → `Complete` | Yes |

---

## Success Criteria Verification

### Acceptance Criteria Summary

| Criterion | Method | Result |
|-----------|--------|--------|
| `arg_count` eliminated from `src/` | `Select-String "arg_count"` in `src/` | **PASS** — 0 matches |
| `no_choices` eliminated from `src/` | `Select-String "no_choices"` in `src/` | **PASS** — 0 matches |
| `type_error` fixed in `src/` | `Select-String "type_error"` in `src/` | **PASS** — 0 matches |
| Existing tests updated | File inspection | **PASS** — 2 assertions updated |
| New tests cover arg-count paths | `dotnet test` | **PASS** — 6 new tests, all green |
| Full test suite passes | `dotnet test` | **PASS** — 682 passed, 0 failed |

**Overall: 6/6 criteria passed.**

---

## Deviations from Plan

### Deviation 1: Step 5 test file placement for Jump/Fork/Call
- **Planned:** Add to `FlowErrorTests.cs`
- **Actual:** Added to `ConcurrencyTests.cs`
- **Reason:** Jump/Fork/Call drivers cast `IExecutionContext` to `Context`. `FlowErrorTests.cs` uses `TestExecutionContext`, which fails the cast and produces `invalid_context` instead of `invalid_params`. `ConcurrencyTests.cs` already has a proper `Context` factory — the natural home for these tests.
- **Impact:** None — same paths covered, same assertions.

### Deviation 2: Step 5 test file placement for Wait/Signal
- **Planned:** Add to `ConcurrencyTests.cs` (or new file)
- **Actual:** Added to `SleepTests.cs`
- **Reason:** `SleepTests.cs` has a bare `Context` factory with `SignalManager` wired up, which is exactly what `WaitDriver` and `SignalDriver` need. `ConcurrencyTests.cs` was already used for Jump/Fork/Call.
- **Impact:** None — all 6 new tests are in adjacent logical groupings.

---

## Issues Encountered

None.

---

## Key Insights

### Lessons Learned

1. **`TestExecutionContext` vs `Context` matters for driver tests**
   - Drivers that cast `IExecutionContext` to `Context` cannot be tested with `TestExecutionContext` for their missing-arg path — you'll get `invalid_context` first.
   - Use the `Context` factory pattern from `ConcurrencyTests.cs` or `SleepTests.cs` for those drivers.

### Technical Insights

- `ChooseFromDriver.cs` had a latent bug: `.Fatal()` was called with `DiagnosticSeverity.Error`. The fatal wrapper wins at runtime, but the severity field was misleading. Fixed by aligning to `Fatal`.
- The `arg_count` code was entirely untested — the new tests fill that gap.

---

## Recommendations

### Immediate Follow-ups
- [ ] Update `impl/10_std_verbs.md` to remove the `no_choices` code from the spec entry for `/choose` and `/chooseFrom` (noted as out-of-scope in the plan; tracked separately)

### Future Considerations
- Define `invalid_params` in the impl spec as a canonical code alongside `parameter_not_found` and `invalid_type`

---

## Related Projex

- **Plan:** 202603201757-diagnostic-code-invalid-params-plan.md → moved to `closed/`
- **Related:** 20260316-spec-catchup-followup.md (tracks spec-side follow-up)
- **Related:** 20260316-presentation-verb-diagnostics-alignment-plan.md (prior work on same drivers)
