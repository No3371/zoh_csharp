# Walkthrough: Align Run(Context) with Spec — Drop Story Param, Move Loop to Context

> **Execution Date:** 2026-02-27
> **Completed By:** Agent
> **Source Plan:** `20260226-runtime-run-context-align-plan.md`
> **Duration:** ~10 minutes
> **Result:** Success

---

## Summary

Removed the redundant `CompiledStory` parameter from `ZohRuntime.Run()` and moved the execution loop body into a new `Context.Run()` method, aligning the C# implementation with the spec (`impl/09_runtime.md`). `ZohRuntime.Run(ctx)` is now a one-line shim delegating to `ctx.Run()`. All 592 tests pass with 0 build errors.

---

## Objectives Completion

| Objective | Status | Notes |
|-----------|--------|-------|
| `Run(Context ctx)` signature — no `story` param | Complete | Signature is now `public void Run(Context ctx)` |
| Execution loop body in `Context.Run()` | Complete | Full loop extracted into `Context.Run()` |
| `ZohRuntime.Run(ctx)` delegates to `ctx.Run()` | Complete | Body is `ctx.Run();` only |
| All test files compile and pass | Complete | 592/592 pass |
| `dotnet build` zero errors | Complete | 0 errors, 0 warnings |
| `dotnet test` all existing tests pass | Complete | 592 passed, 0 failed |

---

## Execution Detail

### Step 1: Add StatementExecutor Delegate to Context and Wire in ZohRuntime

**Planned:** Add `Func<IExecutionContext, VerbCallAst, VerbResult>? StatementExecutor` to `Context`, add `ExecuteStatement` private method to `ZohRuntime`, wire in `CreateContext()`.

**Actual:** Exactly as planned. Also propagated `StatementExecutor` in `Context.Clone()` (discovered during implementation — not in plan but logically required so forked contexts retain dispatch capability).

**Deviation:** Minor: `Clone()` update was not listed in the plan's step 1 but aligns with the existing pattern for `VerbExecutor`, `StoryLoader`, etc.

**Files Changed:**
| File | Change Type | Planned? | Details |
|------|-------------|----------|---------|
| `src/Zoh.Runtime/Execution/Context.cs` | Modified | Yes | Added `StatementExecutor` delegate property (line 28); propagated in `Clone()` |
| `src/Zoh.Runtime/Execution/ZohRuntime.cs` | Modified | Yes | Added `ExecuteStatement()` private method; added `ctx.StatementExecutor = ExecuteStatement;` in `CreateContext()` |

### Step 2: Add `Run()` Method to Context

**Planned:** Add `public void Run()` to `Context` containing a direct extraction of the loop from `ZohRuntime.Run()`, using `StatementExecutor!` for dispatch.

**Actual:** Exactly as planned. No new `using` directives needed — `StatementAst`, `DiagnosticSeverity`, `CompiledStory`, etc. were already imported via existing usings.

**Deviation:** None.

**Files Changed:**
| File | Change Type | Planned? | Details |
|------|-------------|----------|---------|
| `src/Zoh.Runtime/Execution/Context.cs` | Modified | Yes | Added `Run()` method (~65 lines), placed after `ExecuteVerb()` |

**Verification:** Loop logic confirmed to be an exact structural copy of the old `ZohRuntime.Run()` inner while-body.

### Step 3: Slim Down `ZohRuntime.Run()` to Thin Shim

**Planned:** Replace `Run(Context ctx, CompiledStory story)` body (~90 lines) with `public void Run(Context ctx) { ctx.Run(); }`.

**Actual:** Exactly as planned. Old `story` parameter removed entirely along with the loop body.

**Deviation:** None.

**Files Changed:**
| File | Change Type | Planned? | Details |
|------|-------------|----------|---------|
| `src/Zoh.Runtime/Execution/ZohRuntime.cs` | Modified | Yes | `Run(ctx, story)` replaced with `Run(ctx) { ctx.Run(); }` — net ~88 lines deleted |

### Step 4: Update All Test Call Sites

**Planned:** Mechanically update all `runtime.Run(ctx, story)` → `runtime.Run(ctx)` across 13 test files.

**Actual:** Required two passes. First pass (literal `runtime.Run(ctx, story)`) caught 5 files. Build caught 8 Media test files that used `context` as variable name — second pass used regex `.Run(\w+, \w+)` → `.Run($1)`.

**Deviation:** Plan's grep only matched `ctx` variable. Media tests use `context`. Both passes produced the same semantic change — mechanical rename, no logic difference.

**Files Changed:**
| File | Change Type | Planned? |
|------|-------------|----------|
| `tests/Zoh.Tests/Execution/RuntimeTests.cs` | Modified | Yes |
| `tests/Zoh.Tests/Verbs/Standard/Presentation/ChooseDriverTests.cs` | Modified | Yes |
| `tests/Zoh.Tests/Verbs/Standard/Presentation/ChooseFromDriverTests.cs` | Modified | Yes |
| `tests/Zoh.Tests/Verbs/Standard/Presentation/ConverseDriverTests.cs` | Modified | Yes |
| `tests/Zoh.Tests/Verbs/Standard/Presentation/PromptDriverTests.cs` | Modified | Yes |
| `tests/Zoh.Tests/Verbs/Standard/Media/HideDriverTests.cs` | Modified | Yes |
| `tests/Zoh.Tests/Verbs/Standard/Media/PauseDriverTests.cs` | Modified | Yes |
| `tests/Zoh.Tests/Verbs/Standard/Media/PlayDriverTests.cs` | Modified | Yes |
| `tests/Zoh.Tests/Verbs/Standard/Media/PlayOneDriverTests.cs` | Modified | Yes |
| `tests/Zoh.Tests/Verbs/Standard/Media/ResumeDriverTests.cs` | Modified | Yes |
| `tests/Zoh.Tests/Verbs/Standard/Media/SetVolumeDriverTests.cs` | Modified | Yes |
| `tests/Zoh.Tests/Verbs/Standard/Media/ShowDriverTests.cs` | Modified | Yes |
| `tests/Zoh.Tests/Verbs/Standard/Media/StopDriverTests.cs` | Modified | Yes |

---

## Complete Change Log

> **Derived from:** `git diff --stat main..HEAD`

### Files Created
| File | Purpose | In Plan? |
|------|---------|----------|
| `projex/20260226-runtime-run-context-align-log.md` | Execution log | No (workflow artifact) |

### Files Modified
| File | Changes | In Plan? |
|------|---------|----------|
| `src/Zoh.Runtime/Execution/Context.cs` | Added `StatementExecutor` delegate, `Run()` method, propagated in `Clone()` | Yes |
| `src/Zoh.Runtime/Execution/ZohRuntime.cs` | Added `ExecuteStatement()` + wiring, replaced `Run(ctx, story)` with thin shim | Yes |
| 13 test files | `Run(ctx/context, story)` → `Run(ctx/context)` | Yes |
| `projex/20260226-runtime-run-context-align-plan.md` | Status: Ready → In Progress → Complete | Workflow |

---

## Success Criteria Verification

### Acceptance Criteria Summary

| Criterion | Method | Result | Evidence |
|-----------|--------|--------|----------|
| `Run(Context ctx)` signature, no `story` param | Inspect `ZohRuntime.cs` | **Pass** | `public void Run(Context ctx)` — single param |
| Loop in `Context.Run()` | Inspect `Context.cs` | **Pass** | Full while-loop present in `Context.Run()` |
| Runtime is thin shim | Inspect `ZohRuntime.Run()` | **Pass** | Body is `ctx.Run();` only |
| `dotnet build` zero errors | `dotnet build` | **Pass** | `建置成功。0 個警告 0 個錯誤` |
| `dotnet test` all pass | `dotnet test` | **Pass** | `通過! 失敗:0，通過:592，略過:0，總計:592` |
| No stale `Run(ctx, story)` references | grep across codebase | **Pass** | 0 matches found |

**Overall: 6/6 criteria passed**

---

## Deviations from Plan

### Deviation 1: Media tests use `context` var, not `ctx`
- **Planned:** Grep pattern `runtime.Run(ctx, story)` implied all tests use `ctx`.
- **Actual:** 8 Media test files use `context`. Required a second broader regex pass.
- **Reason:** Plan's grep example was illustrative, not exhaustive — variable names differ across test classes.
- **Impact:** Required one extra PowerShell command. No functional difference.
- **Recommendation:** Future plans should use a regex grep pattern to catch all variable names.

### Deviation 2: `Clone()` also updated with `StatementExecutor`
- **Planned:** Plan did not mention updating `Clone()`.
- **Actual:** `StatementExecutor = StatementExecutor` added to `Clone()` initializer.
- **Reason:** `VerbExecutor`, `StoryLoader`, `ContextScheduler` were all cloned — omitting `StatementExecutor` would have broken forked contexts.
- **Impact:** Positive. Ensures fork/call semantics work correctly.
- **Recommendation:** Plan should have included this — add it to any future similar plans.

---

## Issues Encountered

### Issue 1: Media test files missed by first grep pass
- **Description:** The initial PowerShell replace targeted literal `runtime.Run(ctx, story)`. Media tests use `runtime.Run(context, story)`.
- **Severity:** Low — caught immediately by the compiler.
- **Resolution:** Second PowerShell pass using regex pattern `.Run(\w+, \w+)` → `.Run($1)`.
- **Time Impact:** < 1 minute.
- **Prevention:** Use regex grep in pre-execution verification and in replace operations.

---

## Key Insights

### Lessons Learned

1. **Variable naming in test files is not uniform**
   - Context: Media tests use `context`, others use `ctx`.
   - Insight: Mechanical renames across large test suites should use regex, not literal string matching.
   - Application: Always use regex for cross-file call-site updates.

2. **`Clone()` must mirror all injected delegates**
   - Context: Forked contexts (via `/fork`, `/call`) are created through `Clone()`.
   - Insight: Any new delegate property added to `Context` must also be propagated in `Clone()`, or forked child contexts will silently break.
   - Application: When adding delegates to `Context`, always update `Clone()` as part of the same change.

### Technical Insights

- `Context.Run()` now owns the execution loop — the `StatementExecutor` delegate bridges the dependency gap cleanly without `Context` needing a VerbRegistry reference.
- The two dispatch paths remain cleanly separate: `VerbExecutor` (for `/do` verb-as-value) and `StatementExecutor` (for statement-level verb calls in the execution loop).
- The `ExistingVerbRegistry.GetDriver()` null case (unknown verb) returns `VerbResult.Ok()` in `ExecuteStatement()`, matching the old behavior.

---

## Recommendations

### Immediate Follow-ups
- [ ] Execute `20260226-runtime-run-to-completion-plan.md` (unblocked by this change)
- [ ] Execute `20260226-runtime-run-to-completion-shorthand-plan.md` (also unblocked)

### Plan Improvements
- Add `Clone()` to the list of files needing update whenever delegates are added to `Context`
- Use regex grep patterns for multi-variable-name call site updates

---

## Related Projex Updates

### Documents to Update
| Document | Update Needed |
|----------|---------------|
| `20260226-runtime-run-context-align-plan.md` | Mark Complete, link walkthrough ✓ |
| `20260226-runtime-run-to-completion-plan.md` | Now unblocked — dependency satisfied |
| `20260226-runtime-run-to-completion-shorthand-plan.md` | Now unblocked — dependency satisfied |

---

## Appendix

### Test Output
```
已通過! - 失敗:     0，通過:   592，略過:     0，總計:   592，持續時間: 228 ms - Zoh.Tests.dll (net8.0)
```

### Commits
```
d10bf77 projex: step 1-3 - add StatementExecutor, Context.Run(), slim ZohRuntime.Run()
f76262d projex: step 4 - update all 13 test files to Run(ctx) — drop story arg
0ba8069 projex: finalize execution log and mark plan Complete
```
