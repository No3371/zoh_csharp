# Core Verb Namespace Alignment

> **Status:** Complete
> **Created:** 2026-03-24
> **Completed:** 2026-03-25
> **Walkthrough:** `2603241530-core-verb-namespace-alignment-walkthrough.md`
> **Author:** Claude
> **Source:** Follow-up from `2603201530-core-verb-namespace-restructure-plan.md` (spec)
> **Worktree:** No

---

## Summary

Align the C# runtime's verb driver `Namespace` properties and directory structure with the spec's new `Core.{Group}.{Name}` naming convention. Currently, 25 drivers use flat `"core"`, 4 use `"store"`, 4 use `"channel"`, and 3 use `null`. After this change, every core driver reports a two-level namespace like `"core.var"`, `"core.flow"`, `"core.nav"`, etc.

**Scope:** `src/Zoh.Runtime/Verbs/` directory restructure + driver property updates + registration fixes. Standard verbs (`Standard/`) unchanged.
**Estimated Changes:** ~45 driver files moved/updated, 1 registry file, ~3 test files.

---

## Objective

### Problem / Gap / Need

The spec restructured all core verb headings into `Core.{Group}.{Name}` (11 groups, 52 verbs). The C# runtime still uses the old flat layout:

| C# Directory | Driver `Namespace` | Verb Count | Problem |
|---|---|---|---|
| `Core/` | `"core"` | 25 | Flat — no group distinction |
| `Flow/` | `"core"` | 10 | Mixes flow, nav, and signal verbs |
| `Store/` | `"store"` | 4 | Missing `core.` prefix |
| `Signals/` | `null` | 2 | No namespace at all |
| `ChannelVerbs.cs` | `"channel"` | 4 | Missing `core.` prefix, 4 classes in one file |
| `Flow/SleepDriver.cs` | `null` | 1 | Wrong directory, no namespace |

### Success Criteria

- [x] Every core verb driver's `Namespace` property matches its spec group (e.g., `"core.var"`, `"core.flow"`)
- [x] Directory structure mirrors spec groups: `Var/`, `Eval/`, `Flow/`, `Nav/`, `Collection/`, `Math/`, `Store/`, `Channel/`, `Signal/`, `Error/`, `Debug/`
- [x] C# namespace declarations match directory paths (`Zoh.Runtime.Verbs.{Group}`)
- [x] `VerbRegistry.RegisterCoreVerbs()` compiles with updated references
- [x] `dotnet build` succeeds
- [x] `dotnet test` passes (all existing tests)
- [x] Suffix resolution still works: `/set;`, `/write;`, `/sleep;` etc. resolve correctly

### Out of Scope

- Standard verbs (`Standard/Media/`, `Standard/Presentation/`) — unchanged
- Infrastructure files (`IVerbDriver.cs`, `VerbRegistry.cs`, `DriverResult.cs`, `Continuation.cs`, `WaitOutcome.cs`, `WaitRequest.cs`) — stay in `Verbs/` root
- Splitting multi-class files (`CollectionDrivers.cs` has Insert/Remove/Clear) — cosmetic, deferred
- Spec changes — completed in prior projex
- New driver implementations or behavior changes

---

## Context

### Current State

The `Verbs/` directory has 3 subdirectories for core verbs (`Core/`, `Flow/`, `Signals/`) plus `Store/`, plus `ChannelVerbs.cs` at root level. 46 core verb driver classes spread across these locations.

The `VerbRegistry` uses suffix-based resolution — changing `Namespace` from `"core"` to `"core.var"` makes `/set;` still resolve (suffix `"set"` matches), and adds new resolution paths `/var.set;` and `/core.var.set;`. The only breakage is scripts using the intermediate form `/core.set;` which would need `/core.var.set;` instead.

### Key Files

| File | Role | Change Summary |
|------|------|----------------|
| `src/Zoh.Runtime/Verbs/Core/*.cs` (25 files) | Flat core drivers | Move to group dirs, update namespaces |
| `src/Zoh.Runtime/Verbs/Flow/*.cs` (10 files) | Flow/Nav/Signal mix | Split across Flow, Nav, Signal dirs |
| `src/Zoh.Runtime/Verbs/Store/*.cs` (4 files) | Store drivers | Update `Namespace` to `"core.store"` |
| `src/Zoh.Runtime/Verbs/Signals/*.cs` (2 files) | Signal drivers | Move to `Signal/`, update namespace |
| `src/Zoh.Runtime/Verbs/ChannelVerbs.cs` | 4 channel drivers | Move to `Channel/`, update namespace |
| `src/Zoh.Runtime/Verbs/VerbRegistry.cs` | Registration | Update `using` directives and qualified names |
| `tests/Zoh.Tests/Verbs/NamespaceTests.cs` | Namespace resolution tests | Verify, possibly update assertions |

### Dependencies

- **Requires:** `2603201530-core-verb-namespace-restructure-plan.md` (spec, Complete)
- **Blocks:** Nothing

### Constraints

- Suffix resolution must remain backward-compatible for short verb names (`/set;`, `/write;`, `/open;`, `/sleep;`)
- Multi-class files (`CollectionDrivers.cs` with Insert/Remove/Clear) keep their structure — splitting is out of scope
- `FlowUtils.cs` stays in `Flow/` (shared utility for flow drivers; Nav drivers that reference it import from `Verbs.Flow`)

### Assumptions

- `ChannelVerbs.cs` contains exactly 4 driver classes (`OpenVerbDriver`, `PushVerbDriver`, `PullVerbDriver`, `CloseVerbDriver`) — verified via `rg`
- `FlowUtils` is only referenced by Flow-group drivers (Foreach, Loop, Sequence) — verified, no Nav cross-reference
- No external consumers depend on the C# namespace `Zoh.Runtime.Verbs.Core` (only internal + test references)

### Impact Analysis

- **Direct:** ~45 driver files renamed/moved, 1 registry file updated
- **Adjacent:** Test files importing moved namespaces; `FlowUtils` cross-referenced from Nav drivers
- **Downstream:** Any external runtime hosts that manually register or reference drivers by C# namespace (unlikely — registration is internal)

---

## Implementation

### Overview

Four steps: (1) move files to spec-aligned directories, (2) update C# namespaces and `Namespace` properties, (3) update the registry, (4) fix tests. Each step is committed atomically.

### Step 1: Create Directories and Move Files

**Objective:** Restructure `Verbs/` to match spec groups.

**Confidence:** High

**Depends on:** None

**File Move Map:**

#### New directories to create
`Var/`, `Eval/`, `Nav/`, `Collection/`, `Math/`, `Channel/`, `Signal/`, `Error/`, `Debug/`

(`Flow/` and `Store/` already exist.)

#### Moves from `Core/` (25 files → 8 directories)

| File | Destination | Spec Group |
|------|-------------|------------|
| `SetDriver.cs` | `Var/` | core.var |
| `GetDriver.cs` | `Var/` | core.var |
| `DropDriver.cs` | `Var/` | core.var |
| `CaptureDriver.cs` | `Var/` | core.var |
| `TypeDriver.cs` | `Var/` | core.var |
| `CountDriver.cs` | `Var/` | core.var |
| `FlagDriver.cs` | `Var/` | core.var |
| `ParseDriver.cs` | `Var/` | core.var |
| `EvaluateDriver.cs` | `Eval/` | core.eval |
| `InterpolateDriver.cs` | `Eval/` | core.eval |
| `DoDriver.cs` | `Flow/` | core.flow |
| `AppendDriver.cs` | `Collection/` | core.collection |
| `CollectionDrivers.cs` | `Collection/` | core.collection |
| `HasDriver.cs` | `Collection/` | core.collection |
| `AnyDriver.cs` | `Collection/` | core.collection |
| `FirstDriver.cs` | `Collection/` | core.collection |
| `IncreaseDriver.cs` | `Math/` | core.math |
| `DecreaseDriver.cs` | `Math/` | core.math |
| `RollDriver.cs` | `Math/` | core.math |
| `TryDriver.cs` | `Error/` | core.error |
| `DeferDriver.cs` | `Error/` | core.error |
| `DiagnoseDriver.cs` | `Debug/` | core.debug |
| `DebugDriver.cs` | `Debug/` | core.debug |
| `AssertDriver.cs` | `Debug/` | core.debug |

(After all moves, `Core/` directory is empty and can be deleted.)

#### Moves from `Flow/` (3 files → Nav, 1 → Signal)

| File | Destination | Spec Group |
|------|-------------|------------|
| `JumpDriver.cs` | `Nav/` | core.nav |
| `ForkDriver.cs` | `Nav/` | core.nav |
| `CallDriver.cs` | `Nav/` | core.nav |
| `SleepDriver.cs` | `Signal/` | core.signal |

(Remaining in `Flow/`: `IfDriver`, `SequenceDriver`, `LoopDriver`, `WhileDriver`, `ForeachDriver`, `SwitchDriver`, `ExitDriver`, `FlowUtils.cs`)

#### Moves from `Signals/` (2 files → Signal)

| File | Destination | Spec Group |
|------|-------------|------------|
| `WaitDriver.cs` | `Signal/` | core.signal |
| `SignalDriver.cs` | `Signal/` | core.signal |

(After all moves, `Signals/` directory is empty and can be deleted.)

#### Moves from root `Verbs/`

| File | Destination | Spec Group |
|------|-------------|------------|
| `ChannelVerbs.cs` | `Channel/` | core.channel |

#### No moves (stay in place)

| Directory | Files | Reason |
|-----------|-------|--------|
| `Store/` | WriteDriver, ReadDriver, EraseDriver, PurgeDriver | Already matches spec group name |
| `Flow/` | IfDriver, SequenceDriver, LoopDriver, WhileDriver, ForeachDriver, SwitchDriver, ExitDriver, FlowUtils | Already in correct group |

**Verification:** All `Core/*.cs` driver files moved (directory empty). All `Signals/*.cs` moved. `ChannelVerbs.cs` no longer at root. `git status` shows clean renames.

**If this fails:** `git checkout -- .` to revert all moves.

---

### Step 2: Update Namespaces and Properties

**Objective:** Update every moved/affected driver's C# `namespace` declaration and `Namespace` property to match spec groups.

**Confidence:** High

**Depends on:** Step 1

**Property update map:**

| Directory | C# Namespace | `Namespace` Property | Files |
|-----------|-------------|---------------------|-------|
| `Var/` | `Zoh.Runtime.Verbs.Var` | `"core.var"` | Set, Get, Drop, Capture, Type, Count, Flag, Parse |
| `Eval/` | `Zoh.Runtime.Verbs.Eval` | `"core.eval"` | Evaluate, Interpolate |
| `Flow/` | `Zoh.Runtime.Verbs.Flow` | `"core.flow"` | Do, If, Sequence, Loop, While, Foreach, Switch, Exit |
| `Nav/` | `Zoh.Runtime.Verbs.Nav` | `"core.nav"` | Jump, Fork, Call |
| `Collection/` | `Zoh.Runtime.Verbs.Collection` | `"core.collection"` | Append, CollectionDrivers (Insert/Remove/Clear), Has, Any, First |
| `Math/` | `Zoh.Runtime.Verbs.Math` | `"core.math"` | Increase, Decrease, Roll |
| `Store/` | `Zoh.Runtime.Verbs.Store` | `"core.store"` | Write, Read, Erase, Purge |
| `Channel/` | `Zoh.Runtime.Verbs.Channel` | `"core.channel"` | ChannelVerbs (Open/Push/Pull/Close) |
| `Signal/` | `Zoh.Runtime.Verbs.Signal` | `"core.signal"` | Wait, Signal, Sleep |
| `Error/` | `Zoh.Runtime.Verbs.Error` | `"core.error"` | Try, Defer |
| `Debug/` | `Zoh.Runtime.Verbs.Debug` | `"core.debug"` | Diagnose, Debug, Assert |

**Changes per file (pattern):**

```csharp
// Before:
namespace Zoh.Runtime.Verbs.Core;
// ...
public string Namespace => "core";

// After (example for SetDriver → Var/):
namespace Zoh.Runtime.Verbs.Var;
// ...
public string Namespace => "core.var";
```

**Special cases:**
- `Flow/` drivers that were already there (If, Sequence, etc.): only update `Namespace` from `"core"` to `"core.flow"` — C# namespace stays `Zoh.Runtime.Verbs.Flow`
- `Store/` drivers: only update `Namespace` from `"store"` to `"core.store"` — C# namespace stays
- `ChannelVerbs.cs`: update both C# namespace (from `Zoh.Runtime.Verbs` to `Zoh.Runtime.Verbs.Channel`) and property (from `"channel"` to `"core.channel"`)
- `SleepDriver.cs`, `SignalDriver.cs`, `WaitDriver.cs`: update from `null` to `"core.signal"`
- Nav drivers (`JumpDriver`, `ForkDriver`, `CallDriver`): C# namespace from `Verbs.Flow` to `Verbs.Nav`, property from `"core"` to `"core.nav"`. These import `FlowUtils` — add `using Zoh.Runtime.Verbs.Flow;` if needed.

**Verification:** `rg "Namespace =>" src/Zoh.Runtime/Verbs/` — every core driver reports `"core.{group}"`.

**If this fails:** Revert file contents via `git checkout -- .` (moves from Step 1 remain).

---

### Step 3: Update VerbRegistry

**Objective:** Fix `RegisterCoreVerbs()` to use new namespace-qualified class references.

**Confidence:** High

**Depends on:** Step 2

**File:** `src/Zoh.Runtime/Verbs/VerbRegistry.cs`

**Changes:**

Replace `Core.` qualified references with new directory-based qualifications:

```csharp
// Before:
Register(new Core.SetDriver());

// After:
Register(new Var.SetDriver());
```

Full replacement map for `RegisterCoreVerbs()`:

| Old Reference | New Reference |
|---|---|
| `Core.SetDriver` | `Var.SetDriver` |
| `Core.FlagDriver` | `Var.FlagDriver` |
| `Core.GetDriver` | `Var.GetDriver` |
| `Core.DropDriver` | `Var.DropDriver` |
| `Core.CaptureDriver` | `Var.CaptureDriver` |
| `Core.TypeDriver` | `Var.TypeDriver` |
| `Core.AssertDriver` | `Debug.AssertDriver` |
| `Core.IncreaseDriver` | `Math.IncreaseDriver` |
| `Core.DecreaseDriver` | `Math.DecreaseDriver` |
| `Core.InterpolateDriver` | `Eval.InterpolateDriver` |
| `Core.DebugDriver` | `Debug.DebugDriver` |
| `Core.HasDriver` | `Collection.HasDriver` |
| `Core.AnyDriver` | `Collection.AnyDriver` |
| `Core.FirstDriver` | `Collection.FirstDriver` |
| `Core.AppendDriver` | `Collection.AppendDriver` |
| `Core.RollDriver` | `Math.RollDriver` |
| `Core.ParseDriver` | `Var.ParseDriver` |
| `Core.DeferDriver` | `Error.DeferDriver` |
| `Store.WriteDriver` | `Store.WriteDriver` (unchanged) |
| `Store.ReadDriver` | `Store.ReadDriver` (unchanged) |
| `Store.EraseDriver` | `Store.EraseDriver` (unchanged) |
| `Store.PurgeDriver` | `Store.PurgeDriver` (unchanged) |
| `OpenVerbDriver` | `Channel.OpenVerbDriver` |
| `PushVerbDriver` | `Channel.PushVerbDriver` |
| `PullVerbDriver` | `Channel.PullVerbDriver` |
| `CloseVerbDriver` | `Channel.CloseVerbDriver` |
| `Flow.IfDriver` | `Flow.IfDriver` (unchanged) |
| `Flow.LoopDriver` | `Flow.LoopDriver` (unchanged) |
| `Flow.WhileDriver` | `Flow.WhileDriver` (unchanged) |
| `Flow.ForeachDriver` | `Flow.ForeachDriver` (unchanged) |
| `Flow.SwitchDriver` | `Flow.SwitchDriver` (unchanged) |
| `Flow.SequenceDriver` | `Flow.SequenceDriver` (unchanged) |
| `Flow.JumpDriver` | `Nav.JumpDriver` |
| `Flow.ForkDriver` | `Nav.ForkDriver` |
| `Flow.CallDriver` | `Nav.CallDriver` |
| `Flow.ExitDriver` | `Flow.ExitDriver` (unchanged) |
| `Flow.SleepDriver` | `Signal.SleepDriver` |
| `Signals.WaitDriver` | `Signal.WaitDriver` |
| `Signals.SignalDriver` | `Signal.SignalDriver` |

**Unregistered drivers** (exist as files, not in `RegisterCoreVerbs()` — pre-existing, out of scope):
`DoDriver`, `CountDriver`, `DiagnoseDriver`, `EvaluateDriver`, `TryDriver`. These files are still moved and namespace-updated, but no registration line changes are needed for them.

**Verification:** `dotnet build` succeeds.

**If this fails:** Fix compilation errors from incorrect qualified names.

---

### Step 4: Update Tests

**Objective:** Fix test files that reference old C# namespaces.

**Confidence:** Medium — need to verify test scope during execution.

**Depends on:** Step 3

**Files importing `Zoh.Runtime.Verbs.Core` (need `using` updates):**
- `tests/Zoh.Tests/Verbs/CoreVerbTests.cs`
- `tests/Zoh.Tests/Verbs/VerbSpecComplianceTests.cs`
- `tests/Zoh.Tests/Verbs/NestedMutationTests.cs`
- `tests/Zoh.Tests/Verbs/Core/AssertDriverTests.cs` → `Verbs.Debug`
- `tests/Zoh.Tests/Verbs/Core/CollectionVerbsTests.cs` → `Verbs.Collection`
- `tests/Zoh.Tests/Verbs/Core/ControlFlowVerbsTests.cs` → `Verbs.Flow`
- `tests/Zoh.Tests/Verbs/Core/CountTests.cs` → `Verbs.Var`
- `tests/Zoh.Tests/Verbs/Core/DiagnoseTests.cs` → `Verbs.Debug`
- `tests/Zoh.Tests/Verbs/Core/EvaluateVerbsTests.cs` → `Verbs.Eval`
- `tests/Zoh.Tests/Verbs/Core/FlagDriverTests.cs` → `Verbs.Var`
- `tests/Zoh.Tests/Verbs/Core/ParseTests.cs` → `Verbs.Var`
- `tests/Zoh.Tests/Verbs/Core/RollTests.cs` → `Verbs.Math`
- `tests/Zoh.Tests/Verbs/Core/TryTests.cs` → `Verbs.Error`
- `tests/Zoh.Tests/Runtime/SetVerbSpecTests.cs`
- `tests/Zoh.Tests/Runtime/MapStringKeyTests.cs`

**Files importing `Zoh.Runtime.Verbs.Flow` (may need additional imports for Nav/Signal):**
- `tests/Zoh.Tests/Verbs/Flow/SleepTests.cs` → may need `Verbs.Signal`
- `tests/Zoh.Tests/Verbs/Flow/NavigationTests.cs` → may need `Verbs.Nav`
- `tests/Zoh.Tests/Verbs/Flow/ForkDriverFlagTests.cs` → may need `Verbs.Nav`
- `tests/Zoh.Tests/Verbs/Flow/FlowTests.cs`
- `tests/Zoh.Tests/Verbs/Flow/FlowErrorTests.cs`
- `tests/Zoh.Tests/Verbs/Flow/ConcurrencyTests.cs`
- `tests/Zoh.Tests/Execution/CheckpointContractTests.cs`

**No changes expected:**
- `tests/Zoh.Tests/Verbs/NamespaceTests.cs` — uses synthetic `TestVerbDriver`, no real driver imports

**Verification:** `dotnet test` passes.

---

### Step 5: Build and Verify

**Objective:** Confirm the restructure compiles and all tests pass.

**Confidence:** High

**Depends on:** Step 4

**Commands:**
```bash
cd csharp && dotnet build
cd csharp && dotnet test
```

**Verification:**
- Build: zero errors
- Tests: all pass
- `rg "Namespace =>" src/Zoh.Runtime/Verbs/ --no-heading` — no `"core"` flat, no `"store"` without prefix, no `"channel"` without prefix, no `null`

---

## Verification Plan

### Automated Checks

- [ ] `dotnet build` succeeds with zero errors
- [ ] `dotnet test` — all existing tests pass
- [ ] Every core driver `Namespace` matches `core.{group}` pattern

### Manual Verification

- [ ] Directory structure matches spec groups (11 directories for core)
- [ ] `Core/` and `Signals/` directories deleted (empty after moves)
- [ ] `ChannelVerbs.cs` no longer at `Verbs/` root
- [ ] Spot-check 3-4 drivers across groups — correct C# namespace, correct `Namespace` property

### Acceptance Criteria Validation

| Criterion | How to Verify | Expected Result |
|-----------|---------------|-----------------|
| Driver `Namespace` alignment | `rg "Namespace =>" src/Zoh.Runtime/Verbs/ --no-heading` | All core drivers show `"core.{group}"` |
| Directory structure | `ls src/Zoh.Runtime/Verbs/` | 11 core group dirs + Standard + infra files |
| Build | `dotnet build` | 0 errors |
| Tests | `dotnet test` | All pass |
| Suffix resolution | NamespaceTests pass | Short names still resolve |

---

## Rollback Plan

1. `git checkout -- .` reverts all changes in the csharp repo
2. If partially committed, `git reset --hard HEAD~N` for N commits

---

## Notes

### Risks

- **FlowUtils cross-reference**: Verified — only `ForeachDriver`, `LoopDriver`, and `SequenceDriver` reference `FlowUtils`, and all three stay in `Flow/`. Nav drivers do **not** use it. No cross-directory import needed.
- **ChannelVerbs.cs multi-class file**: Contains 4 classes (`OpenVerbDriver`, `PushVerbDriver`, `PullVerbDriver`, `CloseVerbDriver`). Moving to `Channel/` is straightforward. Class names differ from the `{Name}Driver` convention (`OpenVerbDriver` vs `OpenDriver`). Renaming is out of scope.
- **Unregistered drivers**: `DoDriver`, `CountDriver`, `DiagnoseDriver`, `EvaluateDriver`, and `TryDriver` exist as files but are **not** in `RegisterCoreVerbs()`. This is a pre-existing condition — out of scope. They still get moved and their namespaces updated, but registration is a separate task.
- **Test `using` updates**: 17 test files import old namespaces (`Verbs.Core`, `Verbs.Flow`, `Verbs.Signals`). Most need only `using` directive changes. Compilation will catch any that are missed.

### Open Questions

- (none)
