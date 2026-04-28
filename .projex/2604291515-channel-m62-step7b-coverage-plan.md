# m6.2 Step 7b — channel test matrix + manager coverage

> **Status:** In Progress
> **Created:** 2026-04-29
> **Author:** projex-worker (plan-projex)
> **Source:** Audit Partial on `2604270100-channel-blocking-pull-push-impl-plan.md` — Step 7b deferred per `2604281500-channel-blocking-pull-push-impl-walkthrough.md`
> **Nav:** 20260223-csharp-spec-audit-nav.md
> **Related Projex:** 2604270100-channel-blocking-pull-push-impl-plan.md | 2604270124-channel-blocking-pull-push-redteam.md | 2604281500-channel-blocking-pull-push-impl-walkthrough.md | 2604281500-channel-blocking-pull-push-impl-audit.md
> **Worktree:** Yes — `git status` dirty on `main` (unrelated WIP / untracked); execution should not require clean main checkout

---

## Summary

Closes the **Partial** audit on m6.2 by implementing the **Step 7b** test matrix and `ChannelManagerTests` rows that the squash run deliberately deferred. No runtime behavior change unless tests expose a bug → fix in-place under same branch.

**Scope:** Tests only (`ChannelVerbTimeoutTests`, `ChannelManagerTests`), matching tables in closed plan Step 7b.
**Estimated Changes:** 2 test files | ~11 `[Fact]` verb/integration rows + ~11 manager unit rows (exact rows follow source plan).

---

## Objective

### Problem / Gap / Need

Execution landed Steps 1–6 + 7a; walkthrough lists Step 7b **Deferred**. Audit: correctness High (725 tests), completeness Medium — matrix tests absent; redteam “Should-Fix” items (stale-gen push, `Count` doc, `Resume` collect-then-dispatch) partially unaddressed.

### Success Criteria

- New `[Fact]` rows in `tests/Zoh.Tests/Verbs/Channel/ChannelVerbTimeoutTests.cs` cover blocking pull/push, fast-paths, positive `timeout` via `Tick`, `/close` wake both sides, FIFO mixed modes, terminate cleanup — **names + asserts per** `.projex/closed/2604270100-channel-blocking-pull-push-impl-plan.md` → § Step 7b → `ChannelVerbTimeoutTests.cs` table.
- New tests in `tests/Zoh.Tests/Execution/ChannelManagerTests.cs` cover `OfferPush`/`AttemptPull` matrix rows per same § Step 7b → `ChannelManagerTests.cs` table (incl. stale-gen push behavior row).
- `dotnet test --filter FullyQualifiedName~ChannelVerbTimeoutTests` green → then full `dotnet test Zoh.sln` green (count delta noted in future walkthrough).
- Optional (if time in same PR): one-line or short block comment where `Resume` is issued under `ch.Lock` documenting the serialisation invariant (audit: “undocumented in code”).

### Out of Scope

- m6.3 cross-verb timeout consistency sweep (nav lists under broader Phase 6).
- Spec/runtime changes beyond fixes required for failing tests.
- Duplicate of entire `2604270100` plan — implementation already merged; this plan is **append-only tests**.

---

## Context

### Current State

Blocking `/pull`, rendezvous `/push`, `ChannelWaitCondition`, `ResolveWait` cancel paths, `TryClose` wake, `Terminate` cleanup — shipped per walkthrough. Existing regression suite green; dedicated Step 7b coverage missing.

### Key Files


| File                                                       | Role                                   | Change Summary                                   |
| ---------------------------------------------------------- | -------------------------------------- | ------------------------------------------------ |
| `tests/Zoh.Tests/Verbs/Channel/ChannelVerbTimeoutTests.cs` | Integration-style channel verb stories | Add rows from Step 7b table                      |
| `tests/Zoh.Tests/Execution/ChannelManagerTests.cs`         | Direct manager API                     | Add OfferPush/AttemptPull/TryClose/Cancel matrix |


### Dependencies

- **Requires:** Step 7a patches already in tree (`wait: false` spacing); gate filter `ChannelVerbTimeoutTests` green before bulk 7b additions (same ordering as source plan).
- **Blocks:** Audit closure at “Complete” for m6.2 channel IO evidence set (optional nav tweak after walkthrough).

### Constraints

- Parser: bool params like `wait: false` need **space** after `:` — see walkthrough deviation note.
- Tests must remain deterministic; use established test harness patterns from existing `ChannelVerbTimeoutTests` / `ChannelManagerTests`.

### Assumptions

- Manager API surface in walkthrough still matches `ChannelManager.cs` (if renamed, update plan during execute).

### Impact Analysis

- **Direct:** Test files only.
- **Adjacent:** If a test fails, fix in `ChannelManager` / `ChannelVerbs` / `Context` — same scope as original m6.2.
- **Downstream:** None; public API unchanged.

---

## Implementation

### Overview

Copy exact test names and behavioural assertions from `2604270100-channel-blocking-pull-push-impl-plan.md` § Step 7b (two tables). Implement top-down: verb-level integration tests that exercise runtime + manager-level fast tests.

### Step 1: ChannelVerbTimeoutTests — Step 7b rows

**Objective:** Full matrix from closed plan Step 7b first table.
**Confidence:** High — spec copied from executed plan.
**Depends on:** None (after 7a green gate).

**Files:** `tests/Zoh.Tests/Verbs/Channel/ChannelVerbTimeoutTests.cs`

**Changes:** Add each `[Fact]` listed in source plan table (`Pull_OnEmpty_BlocksThenResumesViaPush` through `FifoOrder_AcrossWaitFalseAndWaitTrue`, plus terminate row if placed here vs manager tests — follow source table placement).

**Verification:** `dotnet test --filter FullyQualifiedName~ChannelVerbTimeoutTests`

**If this fails:** Fix story text (`wait: false` spacing) or harness before adding Step 2.

---

### Step 2: ChannelManagerTests — OfferPush / AttemptPull matrix

**Objective:** Second table in § Step 7b — driver-free unit coverage.
**Confidence:** High
**Depends on:** Step 1 gate green (can parallelise after 7a if preferred; full suite still mandatory at end).

**Files:** `tests/Zoh.Tests/Execution/ChannelManagerTests.cs`

**Changes:** Add tests per matrix (`OfferPush_`*, `AttemptPull_`*, `TryClose_*`, `CancelPuller_*`, generation stale rows). Align fake-context patterns with existing tests in file.

**Verification:** `dotnet test --filter FullyQualifiedName~ChannelManagerTests`

**If this fails:** Align test doubles with current `ChannelManager` method signatures.

---

### Step 3: Full suite + optional Resume comment

**Objective:** Confirm no ordering/hang issues; document lock invariant if doing optional comment pass.

**Files:** `tests/...` (verify) | optionally `src/Zoh.Runtime/Execution/ChannelManager.cs`

**Verification:** `dotnet test Zoh.sln` — all pass.

---

## Verification Plan

### Automated Checks

- `dotnet build Zoh.sln`
- `dotnet test --filter FullyQualifiedName~ChannelVerbTimeoutTests`
- `dotnet test --filter FullyQualifiedName~ChannelManagerTests`
- `dotnet test Zoh.sln`
- `dotnet test --filter FullyQualifiedName~ConcurrencyTests` (sanity per original plan)

### Manual Verification

- Spot-check one new test vs spec `core.channel.push` / `core.channel.pull` wording.

### Acceptance Criteria Validation


| Criterion              | How to Verify            | Expected Result                  |
| ---------------------- | ------------------------ | -------------------------------- |
| Step 7b matrix present | Diff vs § Step 7b tables | Row coverage 1:1                 |
| Audit uplift           | Re-run audit mindset     | Partial → Complete on test scope |
| No regression          | Full suite               | All green                        |


---

## Rollback Plan

Revert test-file commits or `git restore` on branch; runtime unchanged if tests-only.

---

## Notes

### Risks

- **Hang on unpatched 7a stories:** Keep 7a ordering rule — extend `ChannelVerbTimeoutTests` only after its filter run is green.

### Open Questions

- None before execution — stale-gen enum variant (`Closed` vs `Stale`) already flagged in source Step 7b table; implement assert to match current enum.

---

## Next Steps (workflow)

Per `plan-projex.md`: execution handoff is orchestrator-owned — `/execute-projex.md @2604291515-channel-m62-step7b-coverage-plan.md` after plan committed to base (projex convention).