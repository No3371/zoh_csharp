# Fix Phase 4 Control-Flow Audit Gaps (`/if`, `/switch`, `/foreach`, `breakif`, `/do`)

> **Status:** Partial — **4/5** child plans complete; **`2603251602-phase4-foreach-iterator-ref-plan.md`** still open (see Progress).
> **Reviewed:** 2026-03-25 — `2603251430-20260227-phase4-control-flow-gaps-fix-plan-review.md`
> **Review outcome:** Plan reconciled 2026-03-25 — objectives trimmed to remaining gaps (see review).
> **Created:** 2026-02-27
> **Author:** Codex
> **Source:** Direct request — gaps identified in [20260223-csharp-spec-audit-nav.md](20260223-csharp-spec-audit-nav.md) Phase 4
> **Related Projex:** [20260223-csharp-spec-audit-nav.md](20260223-csharp-spec-audit-nav.md)
> **Follow-up memos:** `2603252101-phase4-flowutils-condition-suspend-fatal-memo.md` (sequence/`breakif` memo `2603252100` closed via `2603252220-phase4-sequence-breakif-verb-test-patch.md`)

---

## Split execution (2026-03-25)

Work is divided into **five independently executable plans** (same `csharp/` scope; no ordering dependency). Execute any subset in parallel; run full `dotnet test` after all are merged.

| Child plan | Covers |
|------------|--------|
| `2603251600-phase4-if-verb-subject-else-plan.md` | `/if` verb subject + named `else` — **Complete** (walkthrough) |
| `2603251601-phase4-switch-verb-case-plan.md` | `/switch` verb case operands — **Complete** (closed + walkthrough) |
| `2603251602-phase4-foreach-iterator-ref-plan.md` | `/foreach` iterator as reference — **Open** |
| `2603251603-phase4-flowutils-breakif-verb-plan.md` | `breakif` / `continueif` verb conditions — **Complete** (patch `2603251825-phase4-flowutils-breakif-verb-patch.md`) |
| `2603251604-phase4-do-returned-verb-plan.md` | `/do` second hop (returned verb) — **Complete** (patch `2603251810-phase4-do-returned-verb-patch.md`) |

**Progress (2026-03-25):** Done — `2603251600`–`2603251601` + `2603251603`–`2603251604` **plans closed** under `projex/closed/` (patches/walkthroughs as recorded). Open — `2603251602` `/foreach` iterator reference. Optional follow-up: suspend/fatal propagation for verb conditions in `FlowUtils` vs `IfDriver` (`2603252101-…-memo.md`). Sequence + verb `breakif` test: **done** (`2603252220-phase4-sequence-breakif-verb-test-patch.md`).

**Closure:** Umbrella + audit nav Phase 4 item close when **`2603251602`** lands and the **Success Criteria** below are satisfied (checklist).

---

## Summary

Original Phase 4 control-flow gaps vs spec; **most are fixed** in child executions (2026-03-25).

1. **`IfDriver`:** **Done** — verb subject + named `else` (`2603251600-phase4-if-verb-subject-else-plan.md`).
2. **`SwitchDriver`:** **Done** — verb-valued case operands (`2603251601-phase4-switch-verb-case-plan.md`, closed).
3. **`ForeachDriver`:** **Still open** — iterator must be `ValueAst.Reference` (`2603251602-phase4-foreach-iterator-ref-plan.md`).
4. **`FlowUtils`:** **Done** — `breakif` / `continueif` execute verb conditions (`2603251603` + patch `2603251825`); see memo `2603252101` for suspend/fatal propagation nuance.
5. **`DoDriver`:** **Done** — single follow-up hop when first result is `ZohVerb` (`2603251604` + patch `2603251810`).

Verb `breakif` on **`/sequence`:** regression `Sequence_BreakIfVerb_UsesReturnedBoolean` (`2603252220-phase4-sequence-breakif-verb-test-patch.md`).

**Scope:** Control-flow/runtime driver code and directly related verb tests in `csharp/src/Zoh.Runtime` and `csharp/tests/Zoh.Tests`.
**Estimated Changes:** Same five runtime touchpoints + extensions to `FlowTests.cs` and `ControlFlowVerbsTests.cs` (optional small extra test file).

---

## Objective

### Problem / Gap / Need

**Originally:** spec deviations listed in the five bullets below. **As of 2026-03-25,** items 1–2 and 4–5 are addressed in closed child work; item 3 (`/foreach`) remains.

- `/if` verb subject + named `else` — **fixed** (`2603251600`).
- `/switch` verb **case** operands — **fixed** (`2603251601`).
- `/foreach` iterator reference — **open** (`2603251602`).
- `breakif:` / `continueif:` verb execution — **fixed** (`2603251603`); suspend/fatal vs `IfDriver` tracked in `2603252101-phase4-flowutils-condition-suspend-fatal-memo.md`.
- `/do` returned verb — **fixed** (`2603251604`).

### Success Criteria
- [x] `/if` executes a verb **subject** first; then default `is:true` applies to the **result** and enforces boolean/nothing with fatal `invalid_type` otherwise; comparison uses named `is` when present (existing behavior preserved). (`2603251600`)
- [x] `/if` supports named `else:`; positional third argument remains supported as fallback when named `else` is absent. (`2603251600`)
- [x] `/if *x, is: "a", /then;, else: /else;;` branches correctly using named `else` (named `is` already works). (`2603251600`)
- [x] `/switch` executes verb-valued **case** operands before equality comparison. (`2603251601`)
- [ ] `/foreach *list, *it, /verb;;` accepts a reference iterator without `Variable name must be a string` fatal. (`2603251602` — open)
- [x] `breakif` and `continueif` execute `/verb` conditions and use the returned value for truthiness. (`2603251603` / `2603251825`)
- [x] `/do /verb_returning;;` runs the returned verb and surfaces the second execution’s result. (`2603251604` / `2603251810`)
- [ ] New regression tests cover **all** rows above including foreach; full `dotnet test` passes (pending `2603251602`).

### Out of Scope
- Any Phase 5 (concurrency/signals/context) behavior.
- Broad refactoring of `ValueResolver` or expression engine internals.
- Changes to parser syntax or AST model.

---

## Context

### Current State

- `csharp/src/Zoh.Runtime/Verbs/Flow/IfDriver.cs` — **Updated** — verb subject executed before default boolean/nothing guard; named `else` + positional fallback (`2603251600`).

- `csharp/src/Zoh.Runtime/Verbs/Flow/SwitchDriver.cs` — **Updated** — verb-valued case operands executed before comparison (`2603251601`).

- `csharp/src/Zoh.Runtime/Verbs/Flow/ForeachDriver.cs` — **Still pre-fix in plan terms** — iterator handling per `2603251602` (in progress / not yet merged to this narrative’s “done” set).

- `csharp/src/Zoh.Runtime/Verbs/Flow/FlowUtils.cs` — **Updated** — verb conditions for `breakif` / `continueif` executed; return value used for truthiness (`2603251825`).

- `csharp/src/Zoh.Runtime/Verbs/Flow/DoDriver.cs` — **Updated** — optional second hop when first result value is `ZohVerb` (`2603251810`).

### Key Files

| File | Purpose | Changes |
|------|---------|---------|
| `csharp/src/Zoh.Runtime/Verbs/Flow/IfDriver.cs` | `/if` | **Done** — verb subject + named `else` |
| `csharp/src/Zoh.Runtime/Verbs/Flow/SwitchDriver.cs` | `/switch` | **Done** — verb case operands |
| `csharp/src/Zoh.Runtime/Verbs/Flow/ForeachDriver.cs` | `/foreach` | **Open** — `ValueAst.Reference` iterator (`2603251602`) |
| `csharp/src/Zoh.Runtime/Verbs/Flow/FlowUtils.cs` | `breakif` / `continueif` | **Done** |
| `csharp/src/Zoh.Runtime/Verbs/Flow/DoDriver.cs` | `/do` | **Done** — second hop |
| `csharp/tests/Zoh.Tests/Verbs/Flow/FlowTests.cs` | Flow tests | **Done** for Phase 4 items to date, including `Sequence_BreakIfVerb_UsesReturnedBoolean` (`2603252220`) |
| `csharp/tests/Zoh.Tests/Verbs/Core/ControlFlowVerbsTests.cs` | `/do` tests | **Done** — returned-verb chain |

### Dependencies
- **Requires:** None.
- **Blocks:** Future Phase 4 closure in [20260223-csharp-spec-audit-nav.md](20260223-csharp-spec-audit-nav.md).

### Constraints
- Keep diagnostics consistent with existing style/codes where feasible (`invalid_type`, `parameter_not_found`).
- Preserve current behavior for already-passing cases not covered by gaps.
- Stay inside `csharp/` projex scope only.

---

## Implementation

### Overview

**Authoritative step-by-step instructions live in the five child plans** (`2603251600`–`2603251604`). The steps below are retained as a single-document reference; prefer executing from the split files.

Finish `/if` (verb subject + named `else` on top of existing named `is`), then apply the four other driver fixes, then add regression tests.

---

### Step 1: Complete `/if` subject evaluation and named `else` (`IfDriver.cs`)

**Objective:** (1) After `ValueResolver.Resolve` on the subject, if the result is `ZohVerb`, **execute** it and replace `subject` with the return value **before** default `is:true` boolean/nothing validation and before comparison. (2) Read named `else`; if absent, keep positional third unnamed param as else verb. Preserve existing named `is` and type-keyword `is` behavior.

**Files:**
- `csharp/src/Zoh.Runtime/Verbs/Flow/IfDriver.cs`

**Changes (shape — merge with current implementation):**

```csharp
var subject = ValueResolver.Resolve(call.UnnamedParams[0], context);
if (subject is ZohVerb subjectVerb)
    subject = context.ExecuteVerb(subjectVerb.VerbValue, context).ValueOrNothing; // or .Value per existing driver patterns

// existing: named "is", default ZohBool.True, invalid_type if is omitted and subject not bool/nothing
// existing: type-keyword compare via IsTypeKeyword when compare is ZohStr
// else branch: prefer NamedParams["else"] AST; else if UnnamedParams.Length >= 3 use positional else
```

**Rationale:** `spec/2_verbs.md` — subject `/verb` uses return value; `else` is a named parameter; default comparison requires boolean/nothing on the **evaluated** subject.

**Verification:** Tests in Step 6 for verb subject + named `else` + default `invalid_type` after evaluation.

---

### Step 2: Fix `/switch` Verb Case Evaluation (`SwitchDriver.cs`)

**Objective:** Execute `case` values when they resolve to `ZohVerb`, matching subject evaluation behavior.

**Files:**
- `csharp/src/Zoh.Runtime/Verbs/Flow/SwitchDriver.cs`

**Changes:**

```csharp
// Before:
var caseValue = ValueResolver.Resolve(call.UnnamedParams[caseIndex], context);
if (caseValue.Equals(testValue)) ...

// After:
var caseValue = ValueResolver.Resolve(call.UnnamedParams[caseIndex], context);
if (caseValue is ZohVerb caseVerb)
    caseValue = context.ExecuteVerb(caseVerb.VerbValue, context).Value;

if (caseValue.Equals(testValue)) ...
```

**Rationale:** `spec/2_verbs.md` explicitly requires `/verb` case operands to compare using their return value.

**Verification:** Add test where case is a verb returning the matching value.

---

### Step 3: Fix `/foreach` Iterator Reference Handling (`ForeachDriver.cs`)

**Objective:** Treat the 2nd parameter as a reference AST (`ValueAst.Reference`) and use its name directly.

**Files:**
- `csharp/src/Zoh.Runtime/Verbs/Flow/ForeachDriver.cs`

**Changes:**

```csharp
// Before:
var varNameVal = ValueResolver.Resolve(call.UnnamedParams[1], context);
if (varNameVal.Type != ZohValueType.String) fatal(...)
var varName = varNameVal.AsString().Value;

// After:
if (call.UnnamedParams[1] is not ValueAst.Reference iteratorRef)
    return VerbResult.Fatal(new Diagnostic(..., "invalid_type", "Iterator must be a reference.", ...));

var varName = iteratorRef.Name;
```

Keep current map iteration behavior (single-entry kv pair value) and existing break/continue checks.

**Rationale:** Spec requires iterator to be a reference; resolving it eagerly loses the identifier and breaks valid calls.

**Verification:** Add regression test with `*item` iterator reference.

---

### Step 4: Fix `breakif` Verb Evaluation (`FlowUtils.cs`)

**Objective:** Make `ShouldBreak` execute verb-valued conditions before truthiness check.

**Files:**
- `csharp/src/Zoh.Runtime/Verbs/Flow/FlowUtils.cs`

**Changes:**

```csharp
// Before:
var resolved = ValueResolver.Resolve(val, context);
return resolved.IsTruthy();

// After:
var resolved = ValueResolver.Resolve(val, context);
if (resolved is ZohVerb condVerb)
    resolved = context.ExecuteVerb(condVerb.VerbValue, context).Value;
return resolved.IsTruthy();
```

Apply the same logic to `ShouldContinue` (spec symmetry with `breakif`).

**Rationale:** Spec states `/verb` in `breakif` / `continueif` uses returned value; unexecuted `ZohVerb` is incorrectly truthy today.

**Verification:** Add loop/sequence test using `breakif: /verb;` that should stop only when returned boolean becomes true. Loop + foreach covered in `2603251825`; sequence-named test: `Sequence_BreakIfVerb_UsesReturnedBoolean` in `2603252220`.

---

### Step 5: Fix `/do` “Execute Returned Verb” Semantics (`DoDriver.cs`)

**Objective:** When first execution result is a verb, execute that returned verb and return its result.

**Files:**
- `csharp/src/Zoh.Runtime/Verbs/Flow/DoDriver.cs`

**Changes:**

```csharp
// Before:
if (val is ZohVerb v)
    return context.ExecuteVerb(v.VerbValue, context);

// After:
if (val is not ZohVerb v)
    return fatal(invalid_type);

var first = context.ExecuteVerb(v.VerbValue, context);
if (first.IsFatal) return first;

if (first.Value is ZohVerb returnedVerb)
    return context.ExecuteVerb(returnedVerb.VerbValue, context);

return first;
```

**Rationale:** `spec/2_verbs.md` includes explicit example `/do /verb_returning;;` and states `/do` executes the verb returned by parameter verb.

**Verification:** Add `/do` regression test where first execution returns a verb and second execution returns terminal value.

---

### Step 6: Extend Tests (`FlowTests.cs`, `ControlFlowVerbsTests.cs`)

**Objective:** Lock fixed semantics with focused tests.

**Files:**
- `csharp/tests/Zoh.Tests/Verbs/Flow/FlowTests.cs`
- `csharp/tests/Zoh.Tests/Verbs/Core/ControlFlowVerbsTests.cs`

**Changes:**
- `If_VerbSubjectEvaluatedBeforeBranch` (or equivalent) — e.g. `/if /returns_bool;, /then;;` style.
- `If_UsesNamedElse` — named `else:` with `is:` (named `is` alone need not be re-tested from scratch).
- `If_DefaultComparison_InvalidTypeAfterSubjectEval` — non-bool result when `is` omitted, after any verb subject if applicable.
- `Switch_EvaluatesVerbCaseValues`.
- `Foreach_AcceptsReferenceIterator`.
- `Loop_BreakIfVerb_UsesReturnedBoolean` (and optionally `continueif` mirror); `Sequence_BreakIfVerb_UsesReturnedBoolean` (`2603252220`).
- `Do_ExecutesVerbReturnedByFirstExecution`.

Use existing `TestExecutionContext` patterns and helper drivers already present in test suite to avoid introducing brittle scaffolding.

**Rationale:** Close coverage holes for the remaining semantics above; `Do_ExecutesVerb` already covers first hop only.

**Verification:** Tests pass in targeted and full runs.

---

## Verification Plan

### Automated Checks
- [x] `dotnet test --filter "FullyQualifiedName~FlowTests|FullyQualifiedName~ControlFlowVerbsTests"` (after completed child merges)
- [x] `dotnet test` (same; re-run after `2603251602` lands)

### Manual Verification
- [x] `/if` with verb subject and with named `else:` matches spec examples.
- [x] `/switch` verb **case** matches on executed return value.
- [ ] `/foreach *list, *it, ...` iterator binding works.
- [x] `breakif:` / `continueif:` with `/verb` depend on executed return value.
- [x] `/do` second hop when first run returns a verb.

### Acceptance Criteria Validation

| Criterion | How to Verify | Expected Result |
|-----------|---------------|-----------------|
| `/if` verb subject + named `else` | New tests | **Met** (`2603251600`) |
| `/if` default typing guard | Test with non-bool evaluated subject, `is` omitted | **Met** |
| `/switch` verb case eval | New switch test | **Met** (`2603251601`) |
| `/foreach` reference iterator | New foreach test | **Pending** (`2603251602`) |
| `breakif` / `continueif` verbs | New test(s) | **Met** (`2603251603` / `2603251825`); sequence + verb `breakif` **Met** (`2603252220`) |
| `/do` returned verb | New `/do` test | **Met** (`2603251604`) |
| No regressions | Full test run | **Met** after each merge; final pass when foreach lands |

---

## Rollback Plan

1. Revert edits to the five runtime files listed in Key Files.
2. Revert new/updated tests in `FlowTests.cs` and `ControlFlowVerbsTests.cs`.
3. Re-run targeted tests to confirm baseline behavior restored.

---

## Notes

### Assumptions
- Executing a returned verb in `/do` is a single additional execution step (not unbounded recursion).
- Keeping positional third arg as fallback `else` in `/if` is acceptable for backward compatibility while adding named `else` support.
- Existing `ZohVerb` and `ExecuteVerb(ValueAst, IExecutionContext)` path remains the intended dispatch path for dynamic execution.

### Risks
- **Behavioral compatibility risk:** scripts that accidentally relied on prior incorrect behavior (e.g., positional-only else) may change branch outcomes.
  - **Mitigation:** keep positional else fallback during transition and cover both forms in tests.
- **Control-flow subtlety risk:** `ShouldContinue` alignment with `ShouldBreak` could affect edge cases.
  - **Mitigation:** constrain change to verb-evaluation semantics only and verify existing loop tests.

### Open Questions
- [ ] None.
