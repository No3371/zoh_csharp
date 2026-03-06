# Option A: Handle-Backed `/call [inline]` Join and Variable Copy

> **Status:** In Progress
> **Created:** 2026-03-06
> **Author:** Agent
> **Source:** 20260305-inline-call-variable-copy-proposal.md (Option A selected)
> **Related Projex:** 20260305-inline-call-variable-copy-proposal.md, 20260304-runtime-spec-gaps-memo.md, 20260306-inline-call-handle-join-spec-plan.md, 20260227-phase5-concurrency-context-signal-gaps-fix-plan.md

---

## Summary

Implement Option A in the C# runtime by changing `/call` wait/join wiring from string context IDs to stateful `ContextHandle` references, then use the captured child handle to perform `[inline]` variable copy after child termination. This removes scheduler-list lookups from join fulfillment and makes post-termination data access explicit and deterministic for `CallDriver`.

**Scope:** `csharp/src/Zoh.Runtime/` and `csharp/tests/Zoh.Tests/` only.
**Estimated Changes:** 7 files modified, 0-1 new test files.

---

## Objective

### Problem / Gap / Need

Current C# behavior has three gaps that block spec-accurate `/call [inline]`:

1. `JoinContextRequest` carries `ContextId` (`string`) and `ResolveWait` resolves by scanning `_contexts`, coupling join fulfillment to runtime list membership rather than a stable object reference.
2. `CallDriver` does not implement `[inline]` copyback at all; `onFulfilled` only returns child value.
3. `CallDriver` creates child contexts directly and schedules them through `ContextScheduler`, but scheduled children may not always have a populated `Handle`; this is unsafe for downstream host handlers that now expect `ContextHandle`.

### Success Criteria

- [ ] `JoinContextRequest` and `ContextJoinCondition` use `ContextHandle` instead of context ID strings.
- [ ] `ResolveWait` for `WaitingContext` checks `join.TargetHandle.State` (no `_contexts.FirstOrDefault(...)` lookup).
- [ ] `/call` parses transfer refs (`*var...`) and supports `[inline]` copyback on successful child completion only.
- [ ] Copyback reads variables from the captured child handle/context after termination and writes to parent with preserved scope.
- [ ] Newly scheduled contexts always have `Context.Handle` initialized before first execution.
- [ ] New/updated tests cover handle-based join payload and inline copyback behavior.

### Out of Scope

- Updating `impl/08_concurrency.md` or `impl/09_runtime.md` (spec-doc scope).
- Broader `/fork` and `/jump` variadic transfer alignment (separate Phase 5 follow-up).
- Runtime retention policy redesign for terminated contexts (`_contexts` pruning / weak-reference lifecycle policy).

---

## Context

### Current State

1. **Join payload and wait condition are ID-based.**
   - `WaitRequest.cs`: `JoinContextRequest(string ContextId)`
   - `WaitConditionState.cs`: `ContextJoinCondition(string TargetContextId)`
   - `Context.BlockOnRequest(...)`: stores `ContextJoinCondition(c.ContextId)`
   - `ZohRuntime.ResolveWait(...)`: scans `_contexts` by ID, then returns `WaitCompleted(target?.LastResult ?? Nothing)`.

2. **`CallDriver` has no inline merge path.**
   - It accepts only 1 or 2 positional args.
   - It creates/schedules child context.
   - It returns `Suspend(JoinContextRequest(newCtx.Id), onFulfilled => WaitCompleted => Ok(value))`.
   - `[inline]` attribute is ignored.

3. **Handle lifecycle is inconsistent for scheduled children.**
   - `CreateContextInternal(...)` creates handle + map entry.
   - `AddContext(Context ctx)` currently only appends to `_contexts`.
   - `CallDriver`/`ForkDriver` create child contexts manually and schedule with `ContextScheduler`; these paths do not guarantee `ctx.Handle` initialization.

### Key Files

| File | Role | Change Summary |
|------|------|----------------|
| `csharp/src/Zoh.Runtime/Verbs/WaitRequest.cs` | Wait contract surface | Change `JoinContextRequest` payload from `string` to `ContextHandle` |
| `csharp/src/Zoh.Runtime/Execution/WaitConditionState.cs` | Stored wait conditions | Change `ContextJoinCondition` payload to `ContextHandle` |
| `csharp/src/Zoh.Runtime/Execution/Context.cs` | Blocking mechanics | Store handle-backed join condition in `BlockOnRequest` |
| `csharp/src/Zoh.Runtime/Execution/ZohRuntime.cs` | Scheduler | Resolve waits via `TargetHandle.State`/`TargetHandle.InternalContext.LastResult`; ensure scheduled contexts have handles |
| `csharp/src/Zoh.Runtime/Verbs/Flow/CallDriver.cs` | `/call` behavior | Parse transfer refs, capture child handle, implement `[inline]` variable copyback |
| `csharp/src/Zoh.Runtime/Variables/VariableStore.cs` | Scope-preserving copy utility | Add helper to read value + originating scope for transfer/copyback |
| `csharp/tests/Zoh.Tests/Verbs/Flow/ConcurrencyTests.cs` | Driver-level coverage | Assert handle-based join payload and inline merge behavior |
| `csharp/tests/Zoh.Tests/Execution/ApiSurfaceTests.cs` | Runtime scheduling/handles | Add assertion that scheduled child contexts get handle assignment |

### Dependencies

- **Requires:** 20260305-inline-call-variable-copy-proposal.md accepted with Option A, and 20260306-inline-call-handle-join-spec-plan.md merged.
- **Blocks:** Closing Issue #2 in 20260304-runtime-spec-gaps-memo.md and the `[inline]` portion of 20260227-phase5-concurrency-context-signal-gaps-fix-plan.md.

### Constraints

- Keep existing `WaitOutcome` shape (`WaitCompleted(ZohValue Value)`); Option A here is handle-backed join + driver-owned copyback, not `WaitOutcome` expansion.
- Keep existing diagnostic conventions (`invalid_arg`, `arg_count`, `invalid_story`, `invalid_checkpoint`, etc.).
- Do not introduce new public runtime API surface unless needed for testability.

### Assumptions

- Scope preservation means copying with the variable's originating scope in the source context (`Story` vs `Context`).
- `[inline]` copyback happens only on `WaitCompleted`; `WaitCancelled`/`WaitTimedOut` skip copyback.
- Missing source variables copy back as `Nothing` in story scope unless an explicit scope can be inferred.

### Impact Analysis

- **Direct:** Wait/join contracts and `/call` execution path.
- **Adjacent:** Any code constructing `JoinContextRequest` or reading `ContextJoinCondition`.
- **Downstream:** Concurrency tests and any host integration relying on child contexts with host-blocking verbs (now safer due handle initialization guarantee).

---

## Implementation

### Overview

Execute four slices in order:
1. Change join payload/condition types to `ContextHandle`.
2. Update scheduler logic to consume handle state directly and guarantee handle initialization on scheduled contexts.
3. Implement `/call` transfer parsing plus `[inline]` copyback using captured child handle.
4. Add/adjust tests for regression coverage.

---

### Step 1: Convert Join Payloads to `ContextHandle`

**Objective:** Remove ID-based join coupling from continuation and wait-condition contracts.
**Confidence:** High
**Depends on:** None

**Files:**
- `csharp/src/Zoh.Runtime/Verbs/WaitRequest.cs`
- `csharp/src/Zoh.Runtime/Execution/WaitConditionState.cs`
- `csharp/src/Zoh.Runtime/Execution/Context.cs`

**Changes:**

```csharp
// Before
public sealed record JoinContextRequest(string ContextId) : WaitRequest;
public sealed record ContextJoinCondition(string TargetContextId) : WaitConditionState;

// Context.BlockOnRequest
case JoinContextRequest c:
    WaitCondition = new ContextJoinCondition(c.ContextId);

// After
public sealed record JoinContextRequest(ContextHandle Handle) : WaitRequest;
public sealed record ContextJoinCondition(ContextHandle TargetHandle) : WaitConditionState;

case JoinContextRequest c:
    WaitCondition = new ContextJoinCondition(c.Handle);
```

**Rationale:** Option A relies on stable, stateful handles that survive independent of runtime list scans.

**Verification:** Build compiles with no remaining `JoinContextRequest(...Id...)`/`TargetContextId` references.

**If this fails:** Revert all three files together; mixed ID/handle contracts will not compile.

---

### Step 2: Handle-Based Wait Resolution and Handle Initialization

**Objective:** Resolve join wait by handle state and guarantee `Context.Handle` exists for all scheduled contexts.
**Confidence:** High
**Depends on:** Step 1

**Files:**
- `csharp/src/Zoh.Runtime/Execution/ZohRuntime.cs`

**Changes:**

```csharp
// Before
public void AddContext(Context ctx)
{
    _contexts.Add(ctx);
}

if (ctx.WaitCondition is ContextJoinCondition join)
{
    var target = _contexts.FirstOrDefault(c => c.Id == join.TargetContextId);
    if (target == null || target.State == ContextState.Terminated)
        return new WaitCompleted(target?.LastResult ?? ZohValue.Nothing);
}

// After (shape)
public void AddContext(Context ctx)
{
    ctx.Handle ??= new ContextHandle(ctx);
    _handles[ctx.Id] = ctx.Handle;
    _contexts.Add(ctx);
}

if (ctx.WaitCondition is ContextJoinCondition join)
{
    var target = join.TargetHandle;
    if (target.State == ContextState.Terminated)
        return new WaitCompleted(target.InternalContext.LastResult);
}
```

**Rationale:** Join completion should no longer depend on discoverability in `_contexts`; handle state is the source of truth. Ensuring handles at schedule time also prevents null-handle host callback paths in child contexts.

**Verification:**
- No `_contexts.FirstOrDefault(...TargetContextId...)` remains in `ResolveWait`.
- `AddContext` creates/retains handle before scheduling.

**If this fails:** Revert `ZohRuntime.cs`; Step 1 type changes require scheduler logic to be updated atomically.

---

### Step 3: Implement `/call` Transfer Parsing + `[inline]` Copyback

**Objective:** Make `/call [inline]` functionally complete using captured child handle and post-termination copyback.
**Confidence:** Medium
**Depends on:** Steps 1-2

**Files:**
- `csharp/src/Zoh.Runtime/Verbs/Flow/CallDriver.cs`
- `csharp/src/Zoh.Runtime/Variables/VariableStore.cs`

**Changes:**

```csharp
// VariableStore helper (new)
public bool TryGetWithScope(string name, out ZohValue value, out Scope scope)
{
    // story first, then context; returns source scope
}
```

```csharp
// CallDriver: parse target + transfer refs
// Before: accepts exactly 1 or 2 args
// After (shape):
//  - supports label + refs, or story+label+refs
//  - validates all transfer params are ValueAst.Reference
//  - copies transfer refs parent -> child before ValidateContract
//  - if [inline], captures refs for copyback

ctx.ContextScheduler(newCtx);
var childHandle = newCtx.Handle ??= new ContextHandle(newCtx);

return new DriverResult.Suspend(new Continuation(
    new JoinContextRequest(childHandle),
    outcome => outcome switch
    {
        WaitCompleted c => {
            if (shouldInline)
            {
                // For each requested ref:
                // read from childHandle.InternalContext.Variables via TryGetWithScope
                // write into parent ctx.Variables with preserved scope
            }
            return DriverResult.Complete.Ok(c.Value);
        },
        WaitCancelled x => ...,
        _ => DriverResult.Complete.Ok()
    }));
```

**Rationale:** Capturing child handle in continuation gives deterministic access to child final variable state for `[inline]`, which is exactly the Option A proposal intent.

**Verification:**
- `/call [inline] ..., *var` updates parent variable(s) when child completes normally.
- Cancelled/timed-out outcomes do not apply inline copyback.

**If this fails:** Revert `CallDriver.cs` + `VariableStore.cs` together; partial copy logic can silently corrupt scope semantics.

---

### Step 4: Add Regression Tests

**Objective:** Lock behavior so join payload and inline copyback cannot regress.
**Confidence:** High
**Depends on:** Steps 1-3

**Files:**
- `csharp/tests/Zoh.Tests/Verbs/Flow/ConcurrencyTests.cs`
- `csharp/tests/Zoh.Tests/Execution/ApiSurfaceTests.cs`

**Changes:**

```csharp
// ConcurrencyTests additions (shape)
[Fact]
public void Call_BlocksParent_WithJoinHandleRequest()
{
    // request is JoinContextRequest with handle
    // request.Handle.Id == scheduledChild.Id
}

[Fact]
public void Call_Inline_CopiesSpecifiedVariables_OnCompletedOnly()
{
    // arrange parent var + call [inline]
    // mutate child vars, terminate child
    // invoke onFulfilled(WaitCompleted)
    // assert parent vars copied with expected values
}
```

```csharp
// ApiSurfaceTests addition (shape)
[Fact]
public void AddContext_AssignsHandle_ForManuallyScheduledContext()
{
    // create raw Context, runtime.AddContext(ctx), assert ctx.Handle != null
}
```

**Rationale:** These tests cover the exact regression surface introduced by the Option A change.

**Verification:** New tests fail before code changes and pass after.

**If this fails:** Keep Step 1-3 changes, mark failing assertions explicitly, and patch tests only if they conflict with agreed semantics.

---

## Verification Plan

### Automated Checks

- [ ] `cd csharp && dotnet test --filter "FullyQualifiedName~ConcurrencyTests|FullyQualifiedName~ApiSurfaceTests"`
- [ ] `cd csharp && dotnet test`

### Manual Verification

- [ ] Read `WaitRequest.cs` and confirm `JoinContextRequest` carries `ContextHandle`.
- [ ] Read `ZohRuntime.ResolveWait` and confirm no lookup by child context ID.
- [ ] Read `CallDriver` and confirm inline copyback uses captured child handle path.
- [ ] Confirm scheduled child contexts have non-null `Handle`.

### Acceptance Criteria Validation

| Criterion | How to Verify | Expected Result |
|-----------|---------------|-----------------|
| Join payload is handle-based | Read `WaitRequest.cs`/`WaitConditionState.cs` | No ID-string join contracts remain |
| Wait resolver no longer scans by ID | Read `ZohRuntime.ResolveWait` | Uses `join.TargetHandle.State` |
| Inline copyback implemented | Run new Concurrency inline test | Parent receives child values on success |
| Scope preservation for copied vars | Scope-focused inline test | Story/context scope matches source |
| Child contexts always have handles | New ApiSurface test | `AddContext` leaves `ctx.Handle` non-null |
| No regressions | Full `dotnet test` | 0 failures |

---

## Rollback Plan

1. Revert runtime contract changes (`WaitRequest.cs`, `WaitConditionState.cs`, `Context.cs`, `ZohRuntime.cs`) as one unit.
2. Revert `/call` and variable helper changes (`CallDriver.cs`, `VariableStore.cs`).
3. Revert test updates.
4. Re-run targeted tests to confirm baseline behavior restored.

---

## Notes

### Risks

- **Argument-form ambiguity risk:** Distinguishing `label+refs` vs `story+label+refs` can be ambiguous when params are dynamic references.
  - **Mitigation:** Enforce deterministic parse order and add tests for both explicit forms (`?,"label",*v` and `"label",*v`).
- **Scope-copy risk:** Misapplied scope can silently shadow wrong variables.
  - **Mitigation:** Add explicit story/context scope assertions in inline copy tests.

### Open Questions

- [ ] None.
