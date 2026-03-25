# Phase 5 (narrow): `/jump` and `/fork` variadic variable transfer

> **Status:** **In Progress** — active work is **only** trailing `*var` transfer on **`/jump`** and **`/fork`**. `/call`, `/flag`, and `/wait` are **done** (see **Delivered earlier**).
> **Reviewed:** 2026-03-26 — [2603261500-20260227-phase5-concurrency-context-signal-gaps-fix-plan-review.md](2603261500-20260227-phase5-concurrency-context-signal-gaps-fix-plan-review.md) (review requested plan trim; this document supersedes pre-trim body).
> **Reconciled:** 2026-03-26 — cross-checked `csharp/` tree + `CallDriver` / `JumpDriver` / `ForkDriver`.
> **Created:** 2026-02-27
> **Author:** Codex
> **Source:** Gaps from [20260223-csharp-spec-audit-nav.md](20260223-csharp-spec-audit-nav.md) Phase 5
> **Related Projex:** [20260223-csharp-spec-audit-nav.md](20260223-csharp-spec-audit-nav.md), [20260227-phase4-control-flow-gaps-fix-plan.md](closed/20260227-phase4-control-flow-gaps-fix-plan.md), [20260207-csharp-runtime-nav.md](20260207-csharp-runtime-nav.md) (Git evidence section)

---

## Summary

**Delivered earlier (do not re-implement):**

| Item | Where | `csharp/` git (indicative) |
|------|--------|----------------------------|
| `/call` trailing `*var` + `[inline]` merge | `Verbs/Nav/CallDriver.cs` | `cf4f5bd` (2026-03-07) |
| `/flag` | `Verbs/Var/FlagDriver.cs` + registry | `451d387` (2026-03-16) |
| `/wait timeout:` | `Verbs/Signals/WaitDriver.cs` | `2c74be7` (2026-03-20) |
| `TryGetWithScope` for scope-preserving copy | `Variables/VariableStore.cs` | (used by `CallDriver`) |

**Remaining:**

1. **`/jump`** — accept `story?, checkpoint, *var...` (today: `invalid_params` if not exactly 1–2 unnamed args); apply transfers **before** `ValidateContract(targetLabel)`.
2. **`/fork`** — same trailing refs; after the child `Context` exists, copy from **parent** into **child** with preserved scope, **before** `ValidateContract`, matching `CallDriver`’s transfer loop.

**Estimated touch:** `JumpDriver.cs`, `ForkDriver.cs`, optional small shared helper, tests under `NavigationTests` / `ConcurrencyTests`.

---

## Objective

### Problem

`spec/2_verbs.md` / `impl/08_concurrency.md` expect trailing variable transfer on **`/jump`** and **`/fork`**. Those drivers still only allow one or two unnamed parameters (label, or story + label).

### Success criteria

- [ ] `/jump` accepts trailing `*var` references and applies them before contract validation at the target checkpoint.
- [ ] `/fork` accepts trailing `*var` references and initializes the forked context with those values using the same scope semantics as `CallDriver` (read with `TryGetWithScope`, write with `Set` + that scope).
- [x] `/call` trailing `*var` + `[inline]` — met in `CallDriver`.
- [x] `/flag` — met (`FlagDriver`).
- [x] `/wait timeout:` — met (`WaitDriver`).
- [ ] New/updated tests for `/jump` and `/fork` transfer paths; full `dotnet test` passes.

### Out of scope

- Scheduler / continuation redesign beyond what these drivers need.
- Channel `/pull` (Phase 6).
- Changing `CallDriver`, `FlagDriver`, or `WaitDriver` unless a shared helper forces a trivial shared API tweak.

---

## Context

### Current state

- **`JumpDriver`** (`Verbs/Nav/JumpDriver.cs`, `core.nav` / `jump`): branches on `UnnamedParams.Length` 1 vs 2 only; otherwise fatal `invalid_params` (“Jump requires 1 or 2 arguments.”). No transfer pass; `ValidateContract` runs on the current context after optional story switch.
- **`ForkDriver`** (`Verbs/Nav/ForkDriver.cs`, `core.nav` / `fork`): same arity gate. Child context is clone or fresh store; flags copied via `CopyContextFlagsTo`; no variable transfer from parent refs before `ValidateContract`.
- **Reference pattern:** `CallDriver` collects `ValueAst.Reference` from `paramIndex..Length`, then:

```149:156:S:/Repos/zoh/csharp/src/Zoh.Runtime/Verbs/Nav/CallDriver.cs
        // Transfer params into child context before validating the contract
        foreach (var r in transferRefs)
        {
            if (ctx.Variables.TryGetWithScope(r.Name, out var val, out var scope))
            {
                newCtx.Variables.Set(r.Name, val, scope);
            }
        }
```

Use the same loop for `/fork` (parent `ctx` → child `newCtx`). For `/jump`, target is the **same** `Context` after any story switch: read from pre-switch state if variables must be captured before `ExitStory()`, then `Set` on `ctx` in the destination story scope **before** `ValidateContract` (mirror spec ordering).

### Key files (active scope)

| File | Role |
|------|------|
| `csharp/src/Zoh.Runtime/Verbs/Nav/JumpDriver.cs` | Implement variadic parse + transfer + keep existing story/label behavior. |
| `csharp/src/Zoh.Runtime/Verbs/Nav/ForkDriver.cs` | Same; transfer parent → child before contract check. |
| `csharp/src/Zoh.Runtime/Verbs/Nav/CallDriver.cs` | **Read-only reference** for arity disambiguation + transfer loop. |
| `csharp/src/Zoh.Runtime/Variables/VariableStore.cs` | **Existing** `TryGetWithScope` / `Set`; no new API required unless you extract a shared helper. |
| `csharp/tests/Zoh.Tests/Verbs/Flow/NavigationTests.cs` | `/jump` transfer regression. |
| `csharp/tests/Zoh.Tests/Verbs/Flow/ConcurrencyTests.cs` | `/fork` transfer regression. |

### Dependencies

- **Requires:** None.
- **Blocks:** Phase 5 closure in [20260223-csharp-spec-audit-nav.md](20260223-csharp-spec-audit-nav.md).

### Constraints

- Preserve valid `/jump` / `/fork` story + checkpoint combinations today.
- Trailing transfer args must be **references**; otherwise fatal with an `invalid_type` (or existing family) diagnostic **consistent with `CallDriver`** (“transfer parameters must be references”).
- Arity / shape errors: today nav uses `invalid_params` for wrong counts — stay consistent unless you unify with `CallDriver` deliberately.
- Stay within `csharp/` scope.

---

## Implementation

### Step 1 — Parsing: `story?, checkpoint, *var...`

**Objective:** After the fixed story/label prefix (same semantics as today for the first one or two args), every remaining unnamed argument must be `ValueAst.Reference`.

**Approach:** Reuse the **same disambiguation strategy** as `CallDriver` for telling apart `(label, *refs)` vs `(story, label, *refs)` (see `CallDriver`’s `paramIndex` / `transferRefs` collection). `Jump`/`Fork` do not need `[inline]` or join logic—only the prefix + trailing refs.

**Pitfall:** On cross-story `/jump`, resolve and copy values **before** losing story scope if `ExitStory()` clears story-scoped variables; order operations like `CallDriver` when it switches story (load refs from parent context while still valid, then apply in destination).

**Files:** `JumpDriver.cs`, `ForkDriver.cs`.

---

### Step 2 — `/jump` transfer application

**Objective:** Apply refs to the **current** execution context that will run the target label, **before** `ValidateContract(targetLabel)`.

**Shape:**

- After determining `targetLabel` / optional `targetStoryName` and performing any story switch (`ExitStory`, set `CurrentStory`, etc.), run the `TryGetWithScope` / `Set` loop from the **source** context (pre-jump where needed) into the **destination** `ctx.Variables`.
- Then call `ValidateContract` and set `InstructionPointer` as today.

**Verification:** Navigation test: checkpoint contract requires a variable that only exists if transferred from the jump line.

---

### Step 3 — `/fork` transfer application

**Objective:** After `newCtx` is constructed (clone or fresh), copy transferred variables from **parent** `ctx` to **child** `newCtx` using the same loop as `CallDriver`, **before** `newCtx.ValidateContract(targetLabel)`.

**Verification:** Concurrency test: forked entry label requires a var supplied only via trailing refs; child sees correct value/scope.

---

### Step 4 — Optional shared helper

If duplication is large, extract e.g. `NavTransfer.Apply(IExecutionContext from, VariableStore toStore, IEnumerable<ValueAst.Reference> refs)` next to other nav utilities (only if the repo already has or wants a `Nav` helper type—otherwise keep two small loops matching `CallDriver`).

---

### Step 5 — Tests

| Test idea | Suite file |
|-----------|------------|
| `Jump_TransfersVariablesToTargetCheckpoint` (or equivalent name) | `NavigationTests.cs` |
| `Fork_TransfersSpecifiedVariablesToChild` (or equivalent) | `ConcurrencyTests.cs` |

Add negative cases: non-reference trailing arg → fatal; optional: cross-story transfer / shadowing if spec demands it.

---

## Verification plan

### Automated

- [ ] `dotnet test --filter "FullyQualifiedName~NavigationTests|FullyQualifiedName~ConcurrencyTests"`
- [ ] `dotnet test`

### Manual (optional)

- [ ] Script-level `/jump` with transfers satisfies target contract.
- [ ] `/fork` child receives vars without relying on `[clone]` for those bindings.

### Acceptance matrix

| Criterion | Verify | Expected |
|-----------|--------|----------|
| `/jump` transfer | New/updated Navigation test | Vars present before contract; jump succeeds |
| `/fork` transfer | New/updated Concurrency test | Child has transferred vars at entry |
| No regressions | Full test run | 0 failures |

---

## Rollback

1. Revert changes in `JumpDriver.cs` and `ForkDriver.cs` only (and any new shared helper file).
2. Revert new/edited tests in `NavigationTests.cs` / `ConcurrencyTests.cs`.
3. `dotnet test` to confirm baseline.

---

## Notes

### Assumptions

- Trailing args are variable **references** only; literals after the checkpoint are invalid (same as `CallDriver`).
- `VariableStore.TryGetWithScope` / `Set(name, value, scope)` is sufficient for parity with `/call`.

### Risks

- **Cross-story jump:** Wrong order of `ExitStory` vs capture can drop story-scoped values. **Mitigation:** Snapshot transfers before clearing story scope; add a cross-story test if applicable.
- **Arity ambiguity:** Must match `CallDriver`’s rules so scripts parse consistently across `call` / `jump` / `fork`.

### Open questions

- [ ] None.

---

## Appendix — Original five-gap scope (historical)

The 2026-02-27 audit listed `/jump`, `/fork`, `/call`, `/flag`, and `/wait` gaps. The latter three plus `VariableStore` scope helpers are **implemented** in `main`. This plan file was narrowed to the two remaining nav verbs; the old step-by-step for `/call`, `/flag`, `/wait`, and new `FlagTests` / `WaitDriverTests` files is **obsolete**—see **Delivered earlier** and git history.
