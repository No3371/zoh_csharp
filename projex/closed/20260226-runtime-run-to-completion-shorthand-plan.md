# Add RunToCompletion(string) Convenience Shorthand

> **Status:** Complete
> **Created:** 2026-02-26
> **Author:** Agent
> **Source:** `20260226-zohruntime-run-context-design-eval.md` (Opportunity 3)
> **Related Projex:** `20260226-zohruntime-run-context-design-eval.md`, `20260226-runtime-run-context-align-plan.md`, `20260226-runtime-run-to-completion-plan.md`
> **Completed Via:** `20260227-runtime-run-to-completion-shorthand-patch.md`

---

## Summary

Add a top-level `ZohRuntime.RunToCompletion(string source)` convenience method that encapsulates the full 3-step pipeline (load, create context, run) into a single call. Returns the finished `Context` for inspection. This eliminates boilerplate in tests and provides a clean entry point for simple embedder use cases.

**Scope:** `ZohRuntime` class — one new overload method, plus tests
**Estimated Changes:** 1 source file, 1 test file

---

## Objective

### Problem / Gap / Need

Every test and simple embedder must repeat the same 3-line boilerplate:

```csharp
var story = runtime.LoadStory(source);
var ctx = runtime.CreateContext(story);
runtime.Run(ctx);
```

There is no single-call convenience method. The eval document identified this as an opportunities for improving caller ergonomics — a `RunToCompletion(string source)` that handles all three steps.

### Success Criteria

- [x] `ZohRuntime.RunToCompletion(string source)` method exists
- [x] Returns the finished `Context` (not just the value — callers need to inspect variables, diagnostics, state)
- [x] Internally calls `LoadStory` → `CreateContext` → `Run`
- [x] Unit tests verify the method works correctly
- [x] `dotnet build` and `dotnet test` pass

### Out of Scope

- Retrofitting existing tests to use the new shorthand (optional cleanup, not part of this plan)
- Adding overloads with additional parameters (e.g., checkpoint name, initial variables)

---

## Context

### Current State

After the prerequisite plans, `ZohRuntime` will have:
- `Run(Context ctx)` — runs a context (void)
- `RunToCompletion(Context ctx)` — runs and returns value
- `LoadStory(string source)` — compiles source to `CompiledStory`
- `CreateContext(CompiledStory story)` — creates and registers a context

The convenience shorthand chains these together and returns the `Context` for full post-execution inspection.

### Key Files

| File | Purpose | Changes Needed |
|------|---------|----------------|
| `csharp/src/Zoh.Runtime/Execution/ZohRuntime.cs` | Runtime class | Add `RunToCompletion(string)` overload |
| `csharp/tests/Zoh.Tests/Execution/RuntimeTests.cs` | Runtime tests | Add tests for the convenience overload |

### Dependencies

- **Requires:** `20260226-runtime-run-context-align-plan.md` (Run signature), `20260226-runtime-run-to-completion-plan.md` (RunToCompletion(Context) must exist)
- **Blocks:** None

### Constraints

- Return type is `Context` (not `ZohValue`) — tests need to inspect `ctx.Variables`, `ctx.State`, `ctx.LastDiagnostics`, etc.

---

## Implementation

### Overview

Add a single convenience overload to `ZohRuntime` that chains load → create → run and returns the context.

### Step 1: Add `RunToCompletion(string)` Method

**Objective:** Provide a single-call entry point for running ZOH source.

**Files:**
- `csharp/src/Zoh.Runtime/Execution/ZohRuntime.cs`

**Changes:**

Add after the existing `RunToCompletion(Context ctx)` method:

```csharp
// After:
public Context RunToCompletion(string source)
{
    var story = LoadStory(source);
    var ctx = CreateContext(story);
    Run(ctx);
    return ctx;
}
```

**Rationale:** Returns `Context` rather than `ZohValue` because callers (especially tests) routinely inspect `ctx.Variables`, `ctx.State`, `ctx.LastResult`, and `ctx.LastDiagnostics`. Returning only a value would lose this access.

**Verification:** `dotnet build` compiles.

### Step 2: Add Unit Tests

**Objective:** Verify the convenience shorthand works for basic and edge scenarios.

**Files:**
- `csharp/tests/Zoh.Tests/Execution/RuntimeTests.cs`

**Changes:**

```csharp
[Fact]
public void RunToCompletion_String_ExecutesAndReturnsContext()
{
    var runtime = new ZohRuntime();
    var source = @"shorthand test
===
/set *x, 99;
";

    var ctx = runtime.RunToCompletion(source);

    Assert.Equal(ContextState.Terminated, ctx.State);
    Assert.Equal(new ZohInt(99), ctx.Variables.Get("x"));
}

[Fact]
public void RunToCompletion_String_EmptyStory_TerminatesCleanly()
{
    var runtime = new ZohRuntime();
    var source = @"empty
===
";

    var ctx = runtime.RunToCompletion(source);

    Assert.Equal(ContextState.Terminated, ctx.State);
}
```

**Rationale:** Tests cover the primary use case (run script, inspect variables) and the edge case (empty story terminates cleanly).

**Verification:** `dotnet test --filter "FullyQualifiedName~RuntimeTests"` passes.

---

## Verification Plan

### Automated Checks

- [x] `dotnet build` in `csharp/` — zero errors
- [x] `dotnet test` in `csharp/` — all tests pass including new ones
- [x] `dotnet test --filter "FullyQualifiedName~RuntimeTests"` — specifically validates new tests

### Acceptance Criteria Validation

| Criterion | How to Verify | Expected Result |
|-----------|---------------|-----------------|
| Method exists | Inspect `ZohRuntime.cs` | `RunToCompletion(string)` present |
| Returns `Context` | Check return type | Returns `Context` |
| Chains correctly | `RunToCompletion_String_ExecutesAndReturnsContext` test | `ctx.Variables.Get("x")` == 99 |
| Empty story | `RunToCompletion_String_EmptyStory_TerminatesCleanly` test | Terminated state |

---

## Rollback Plan

1. Remove the `RunToCompletion(string)` overload and tests
2. Single commit — `git revert` is clean

---

## Notes

### Assumptions

- `Run(Context ctx)` and `RunToCompletion(Context ctx)` already exist per prerequisite plans
- `LoadStory` handles compilation pipeline including preprocessing, parsing, and validation
- The context returned is the same object added to `_contexts` — callers can inspect it freely

### Risks

- **Naming collision with RunToCompletion(Context):** C# overload resolution handles this cleanly since `string` and `Context` are unambiguous types. No risk.
