# C# Implementation Followup: Spec Catchup

**Date:** 2026-03-16
**Scope:** Gap analysis — recent spec/impl changes vs C# implementation

---

## Already Implemented in C# (no action needed)

| Spec Change | C# Commit | Status |
|---|---|---|
| Two-phase continuation model | `a88c2f8` | Done |
| Interpolation conditional syntax (`$?`, `$#`) | `a2a0dc3` | Done |
| Runtime API surface (ContextHandle, Tick, Resume) | `012ea07` | Done |
| StatementState in Context | `d6179af` | Done |
| Inline call → ContextHandle join fix | `cf4f5bd` | Done |
| Flow verbs error paths + 22 unit tests | `d5bfe11` | Done |
| `[tag]` standard attribute handler passthrough | `843c5e9` | Done |

---

## Gaps: Spec changes without C# implementation

### 1. Channel Semantics & `/try` Suspension Handling
- **Spec commit:** `8a243ee` (spec/impl closed)
- **What changed:** TryDriver now handles suspensions — when the inner verb returns `Suspend`, try wraps the continuation so catch/suppress still apply on resume. New `Suspend` phase between `Execute verb` and `Complete phase`.
- **Files affected:** `impl/07_control_flow.md`, `spec/2_verbs.md`
- **C# impact:** `TryDriver.cs` needs suspension-aware continuation wrapping. Currently try likely drops suspension context.
- **Priority:** High — correctness issue for any `/try` around suspending verbs (`/pull`, `/wait`, presentation verbs).

### 2. Runtime-Scoped Flags
- **Spec commits:** `ce45ab7`, `da860b8`, `04b0997`
- **What changed:** New flag system with two scopes (runtime, context). `/flag` verb writes to context scope by default, `[scope: "runtime"]` writes to runtime. Flag resolution: context → runtime → null. Runtime flags visible to preprocessors. `runtime.flags` map added to Runtime struct. `runtime.setFlag()` / `runtime.getFlag()` API.
- **Files affected:** `spec/1_concepts.md`, `impl/09_runtime.md`
- **C# impact:** Add `flags` dictionary to `ZohRuntime`, add `flags` dictionary to `Context`, implement `/flag` verb driver, implement flag resolution chain, pass runtime flags to preprocessor pipeline.
- **Priority:** High — blocks embed variable interpolation (#3) and `#strsub` (#5).

### 3. Embed Variable Interpolation & Optional `#embed?`
- **Spec commit:** `5c97b07`
- **What changed:** `#embed` paths now support `${name}` interpolation (resolved from built-ins like `filename`, runtime flags, story metadata). New `#embed?` variant silently skips if file not found.
- **Files affected:** `spec/1_concepts.md`, `impl/03_preprocessor.md`
- **C# impact:** Update `EmbedPreprocessor` to interpolate paths before resolution, add `#embed?` handling, wire interpolation context (built-in vars + runtime flags + metadata).
- **Depends on:** #2 (runtime-scoped flags — needed for `${locale}` etc.)
- **Priority:** Medium — enables localization workflows.

### 4. ChooseDriver Alignment
- **Spec commit:** `cd9af5f`
- **What changed:** Major simplification of ChooseDriver in `impl/10_std_verbs.md` (367 lines removed, 100 added) — aligned with verb driver model.
- **C# impact:** Review C# `ChooseDriver` against updated impl spec. May need refactoring to match simplified model.
- **Priority:** Medium — structural alignment.

---

## Plans

| # | Plan | File |
|---|------|------|
| 1 | Runtime-Scoped Flags | `20260316-runtime-scoped-flags-impl-plan.md` |
| 2 | Try Suspension Wrapping | `20260316-try-suspension-wrapping-impl-plan.md` |
| 3 | Embed Variable Interpolation & #embed? | `20260316-embed-variable-interpolation-impl-plan.md` |
| 4 | Presentation Verb Diagnostics Alignment | `20260316-presentation-verb-diagnostics-alignment-plan.md` |
| 5 | [tag] Attribute Handler Support (Complete) | `closed/20260316-tag-attribute-handler-support-plan.md` (patch: `closed/20260316-tag-attribute-handler-support-patch.md`) |

## Recommended Execution Order

1. **Runtime-Scoped Flags** (#1) — prerequisite for #3
2. **Try Suspension Wrapping** (#2) — correctness fix
3. **Embed Variable Interpolation** (#3) — depends on #1
4. **Presentation Verb Diagnostics** (#4) — independent, systemic fix
5. **[tag] Attribute** (#5) — complete (see patch doc)

---

## Open C# Plans (pre-existing, may be stale)

- `20260227-phase4-control-flow-gaps-fix-plan.md`
- `20260227-phase5-concurrency-context-signal-gaps-fix-plan.md`
- `20260226-interpolation-format-regex-free-eval.md`

These should be reviewed for overlap or staleness against the above gaps.
