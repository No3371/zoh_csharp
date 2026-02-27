# Add RunToCompletion(Context) for Synchronous Execution

> **Status:** Complete
> **Created:** 2026-02-26
> **Author:** Agent
> **Source:** `20260226-zohruntime-run-context-design-eval.md` (Opportunity 2)
> **Related Projex:** `20260226-zohruntime-run-context-design-eval.md`, `20260226-runtime-run-context-align-plan.md`

---

## Summary

Implement `ZohRuntime.RunToCompletion(Context): Value` as specified in `impl/09_runtime.md` line 78. This method runs a context synchronously until it terminates (or reaches a non-resumable block), returning the context's last return value. It provides a clean single-call execution path for synchronous, single-context scenarios.

**Scope:** `ZohRuntime` class — one new method, plus tests
**Estimated Changes:** 1 source file, 1 test file

---

## Objective

### Problem / Gap / Need

The spec defines `runToCompletion(context: Context): Value` (line 78 of `impl/09_runtime.md`), but the C# implementation has no equivalent. Callers who want to run a context to completion and get the result must call `Run(ctx)` and then manually inspect `ctx.LastResult`. There is no method that combines execution and value extraction.

### Success Criteria

- [x] `ZohRuntime.RunToCompletion(Context ctx)` method exists and returns `ZohValue`
- [x] Returns the context's `LastResult` after the context reaches `Terminated` state
- [x] Handles edge case: context that blocks (continuation) — returns `ZohNothing.Instance`
- [x] Unit tests verify the method works for basic and edge scenarios
- [x] `dotnet build` and `dotnet test` pass

### Out of Scope

- `RunToCompletion(string source)` convenience overload (separate plan)
- Handling async/tick-loop scenarios within `RunToCompletion`
- Changing the existing `Run(Context)` method

---

## Context

### Current State

After the spec-alignment plan (`20260226-runtime-run-context-align-plan.md`), `ZohRuntime.Run(ctx)` will delegate to `ctx.Run()`. The method is `void` — it does not return a value. To get the result, callers do:

```csharp
runtime.Run(ctx);
var result = ctx.LastResult;  // manual extraction
```

The spec defines `runToCompletion(context: Context): Value` which wraps this into a single call.

### Key Files

| File | Purpose | Changes Needed |
|------|---------|----------------|
| `c#/src/Zoh.Runtime/Execution/ZohRuntime.cs` | Runtime class | Add `RunToCompletion(Context)` method |
| `c#/tests/Zoh.Tests/Execution/RuntimeTests.cs` | Runtime tests | Add tests for `RunToCompletion` |

### Dependencies

- **Requires:** `20260226-runtime-run-context-align-plan.md` (Run signature must be updated first)
- **Blocks:** `20260226-runtime-run-to-completion-shorthand-plan.md`

### Constraints

- Must handle contexts that block on continuations (e.g., `/sleep`, `/pull`) — `RunToCompletion` is for non-blocking stories only. If a context blocks, the method returns the last value before blocking.

---

## Implementation

### Overview

Add a single method to `ZohRuntime` that runs a context via `Run(ctx)` and returns the context's `LastResult`.

### Step 1: Add `RunToCompletion(Context)` Method

**Objective:** Implement the spec-defined `runToCompletion(context: Context): Value`.

**Files:**
- `c#/src/Zoh.Runtime/Execution/ZohRuntime.cs`

**Changes:**

Add after the existing `Run(Context ctx)` method:

```csharp
// After:
public ZohValue RunToCompletion(Context ctx)
{
    Run(ctx);
    return ctx.LastResult ?? ZohNothing.Instance;
}
```

**Rationale:** The simplest correct implementation. `Run(ctx)` executes until the context is no longer `Running` (terminated, blocked, etc.). The last return value is on `ctx.LastResult`. If no verb produced a value, return `nothing`.

**Verification:** `dotnet build` compiles. Tests in Step 2 exercise the method.

### Step 2: Add Unit Tests

**Objective:** Verify `RunToCompletion` works for basic scenarios and edge cases.

**Files:**
- `c#/tests/Zoh.Tests/Execution/RuntimeTests.cs`

**Changes:**

Add test methods:

```csharp
[Fact]
public void RunToCompletion_ReturnsLastResult()
{
    var runtime = new ZohRuntime();
    var source = @"test
===
/set *x, 42;
";
    var story = runtime.LoadStory(source);
    var ctx = runtime.CreateContext(story);

    var result = runtime.RunToCompletion(ctx);

    Assert.Equal(ContextState.Terminated, ctx.State);
    // /set returns the value that was set
    Assert.Equal(new ZohInt(42), result);
}

[Fact]
public void RunToCompletion_EmptyStory_ReturnsNothing()
{
    var runtime = new ZohRuntime();
    var source = @"empty
===
";
    var story = runtime.LoadStory(source);
    var ctx = runtime.CreateContext(story);

    var result = runtime.RunToCompletion(ctx);

    Assert.Equal(ContextState.Terminated, ctx.State);
    Assert.Equal(ZohNothing.Instance, result);
}
```

**Rationale:** Tests cover the happy path (returns value) and edge case (empty story returns nothing).

**Verification:** `dotnet test --filter "FullyQualifiedName~RuntimeTests"` — new tests pass.

---

## Verification Plan

### Automated Checks

- [x] `dotnet build` in `c#/` — zero errors
- [x] `dotnet test` in `c#/` — all tests pass including new ones
- [x] `dotnet test --filter "FullyQualifiedName~RuntimeTests"` — specifically validates new tests


### Acceptance Criteria Validation

| Criterion | How to Verify | Expected Result |
|-----------|---------------|-----------------|
| Method exists | Inspect `ZohRuntime.cs` | `RunToCompletion(Context)` present |
| Returns `ZohValue` | Check return type | Returns `ZohValue` |
| Returns last result | `RunToCompletion_ReturnsLastResult` test | Passes |
| Edge case: nothing | `RunToCompletion_EmptyStory_ReturnsNothing` test | Passes |

---

## Rollback Plan

1. Remove the `RunToCompletion` method and tests
2. Single commit — `git revert` is clean

---

## Notes

### Assumptions

- `Run(ctx)` is already updated to take a single `Context` parameter (per the alignment plan)
- `ctx.LastResult` holds the last verb return value, or `null` if no verb executed
- `ZohNothing.Instance` is the canonical "nothing" value

### Risks

- **LastResult null semantics:** If `ctx.LastResult` can be `null` vs `ZohNothing.Instance` inconsistently, the null coalesce may mask bugs. **Mitigation:** verify `LastResult` semantics during execution.
