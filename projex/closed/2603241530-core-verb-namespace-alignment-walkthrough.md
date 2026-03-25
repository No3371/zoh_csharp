# Walkthrough: Core Verb Namespace Alignment

> **Execution Date:** 2026-03-25  
> **Source Plan:** `2603241530-core-verb-namespace-alignment-plan.md`  
> **Execution Log:** `2603241530-core-verb-namespace-alignment-log.md`  
> **Base Branch:** `main`  
> **Ephemeral Branch:** `projex/2603241530-core-verb-namespace-alignment`  
> **Result:** Success

---

## Summary

Core verb drivers were reorganized under `src/Zoh.Runtime/Verbs/` into spec-aligned groups (`Var`, `Eval`, `Flow`, `Nav`, `Collection`, `Math`, `Store`, `Channel`, `Signal`, `Error`, `Debug`), each exposing `IVerbDriver.Namespace` as `core.{group}`. `VerbRegistry.RegisterCoreVerbs()` and tests were updated. Parser syntactic sugar was aligned so desugared `VerbCallAst` namespaces match the new registration layout (not in the original plan text). All **704** tests pass.

---

## Objectives Completion

| Objective | Status | Notes |
|-----------|--------|-------|
| Driver `Namespace` → `core.{group}` | Complete | No flat `core` / raw `store` / `channel` on core drivers |
| Directory layout mirrors spec groups | Complete | `Core/` and `Signals/` emptied; `Channel/` hosts former root `ChannelVerbs.cs` |
| C# namespaces match folders | Complete | e.g. `Zoh.Runtime.Verbs.Var` |
| Registry compiles and registers drivers | Complete | `VerbRegistry.cs` + usings |
| Build & tests | Complete | `dotnet test` 704/704 |
| Suffix resolution for short names | Complete | `VerbSpecComplianceTests` uses `GetDriver(null, …)`; `NamespaceTests` unchanged |

---

## Execution Detail

### Steps 1–2 (plan): Moves + namespaces

**Planned:** `git mv` map from plan; update `namespace` and `Namespace =>` per group.

**Actual:** Matches plan. Flow drivers retained in `Flow/` with `core.flow`; `DoDriver` moved `Core` → `Flow`; Nav (`Jump`, `Fork`, `Call`) → `Nav/` + `core.nav`; sleep/signal/wait → `Signal/` + `core.signal`; Store → `core.store`; Channel types → `Channel/` + `core.channel`.

**Deviation:** None for file layout.

### Step 3 (plan): VerbRegistry

**Planned:** Replace `Core.*` / `Flow.JumpDriver` / etc. with group-qualified types.

**Actual:** `RegisterCoreVerbs()` uses `Var`, `Eval`, `Math`, `Collection`, `Debug`, `Error`, `Nav`, `Signal`, `Store`, `Channel`, and unqualified `Flow` types where `using Zoh.Runtime.Verbs.Flow` applies.

**Deviation:** None.

### Step 4 (plan): Tests

**Planned:** Update `using` and references in listed test files.

**Actual:** Done; additional `VerbCallAst` namespace strings updated for direct driver tests (e.g. `core.nav` for jump/fork/call, `core.var` for set in `SetVerbSpecTests`, `null` + short name helpers in `CoreVerbTests` where appropriate).

### Parser alignment (unplanned, required)

**Planned:** Not listed.

**Actual:** `Parser.cs` desugaring for `*x <- v`, `<- *x`, `-> *x`, jump/fork/call sugar, `/`expr``, and string interpolate sugar now sets `VerbCallAst.Namespace` to `core.var`, `core.nav`, or `core.eval` instead of `core`, so `VerbResolutionValidator` resolves registered drivers.

**Why:** After drivers moved to `core.var.set`, `GetDriver("core", "set")` / query `core.set` no longer matched; stories with capture/set sugar failed validation (e.g. standard media tests).

**Files:** `src/Zoh.Runtime/Parsing/Parser.cs`; `tests/Zoh.Tests/Parsing/ParserTests.cs`; `tests/Zoh.Tests/Parsing/ParserSpecComplianceTests.cs`.

---

## Complete Change Log

**Authoritative:** `git diff --stat main..projex/2603241530-core-verb-namespace-alignment` (before squash): **69 files**, +270 / −209 lines.

### Runtime (high level)

| Area | Change |
|------|--------|
| `Verbs/` | Renames into `Var`, `Eval`, `Flow`, `Nav`, `Collection`, `Math`, `Store`, `Channel`, `Signal`, `Error`, `Debug` |
| `VerbRegistry.cs` | New registration paths + usings |
| `Parser.cs` | Sugar namespaces `core.var` / `core.nav` / `core.eval` |

### Tests

Broad updates under `tests/Zoh.Tests/` for usings, `VerbSpecComplianceTests`, `VerbCallAst` namespaces, Nav/Signal usings in flow tests.

### Projex

| File | Action |
|------|--------|
| `2603241530-core-verb-namespace-alignment-plan.md` | Complete + criteria checked + walkthrough link |
| `2603241530-core-verb-namespace-alignment-log.md` | Created during execution |
| This walkthrough | Created at close |

---

## Success Criteria Verification

| Criterion | Method | Result |
|-----------|--------|--------|
| All core `Namespace` → `core.{group}` | `rg` on `Verbs/` (excluding `std`) | Pass |
| Eleven group directories + Standard | Directory layout | Pass |
| `dotnet build` | CLI | Pass |
| `dotnet test` | CLI — 704 tests | Pass |
| Suffix resolution | Tests + registry behavior | Pass |

---

## Deviations from Plan

1. **Parser sugar namespaces** — Required for validator/runtime consistency; documented in execution log § Deviations.

---

## Issues Encountered

- Standard verb tests failed with `CompilationException: Validation failed` until parser sugar was fixed (root cause: `core` vs `core.var` in `VerbCallAst`).

---

## Key Insights

- **Desugar output must match registered driver namespaces** when validation uses `GetDriver(call.Namespace, call.Name)` with explicit namespace from AST.
- **Splitting `core` into `core.{group}`** invalidates any hardcoded `"core"` + verb name pairs across parser, tests, and metadata validators that assumed a single `core` segment.

---

## Git Commits (ephemeral branch)

```
e074568 projex: core verb namespace alignment (drivers, registry, parser sugar, tests)
10b1374 projex: start execution of core-verb-namespace-alignment  ← tip of main before squash
```

**Close:** Squash-merge default — single commit on `main` summarizing implementation (plus close commit if committed before squash).

---

## Recommendations

- Consider a short impl note in `impl/` or runtime README: sugar emits `core.var` / `core.nav` / `core.eval` for static validation.
- Optional follow-up: register or document unregistered drivers (`DoDriver`, `CountDriver`, etc.) called out in the plan as pre-existing.
