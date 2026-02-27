# Align Run(Context) with Spec — Drop Story Param, Move Loop to Context

> **Status:** Complete
> **Completed:** 2026-02-27
> **Walkthrough:** `20260226-runtime-run-context-align-walkthrough.md`
> **Created:** 2026-02-26
> **Author:** Agent
> **Source:** `20260226-zohruntime-run-context-design-eval.md` (Opportunity 1)
> **Related Projex:** `20260226-zohruntime-run-context-design-eval.md`

---

## Summary

Remove the redundant `CompiledStory` parameter from `ZohRuntime.Run()` and move the execution loop body into `Context.Run()`, aligning the C# implementation with the spec (`impl/09_runtime.md` lines 77 and 374). After this change, `ZohRuntime.Run(ctx)` becomes a thin orchestration shim that delegates to `ctx.Run()`.

**Scope:** `ZohRuntime.Run()` signature, `Context` class, and all call sites (13 test files, ~30 occurrences)
**Estimated Changes:** 2 source files, 13 test files

---

## Objective

### Problem / Gap / Need

The current `ZohRuntime.Run(Context ctx, CompiledStory story)` has two issues:

1. **Redundant `story` parameter** — `CreateContext(story)` already sets `ctx.CurrentStory = story`. The `story` param in `Run()` is only used as a null-guard fallback (lines 165–168) and diverges from the spec which defines `run(context: Context): void` (spec line 77).

2. **Loop lives in wrong class** — The spec puts the execution loop on `Context.run()` (spec line 374), not on the runtime. The runtime's role is orchestration (scheduling, tick loop), not executing the loop itself.

### Success Criteria

- [ ] `ZohRuntime.Run()` signature is `Run(Context ctx)` — no `story` parameter
- [ ] Execution loop body lives in `Context.Run()` as a public method
- [ ] `ZohRuntime.Run(ctx)` delegates to `ctx.Run()` (thin shim)
- [ ] All 13+ test files compile and pass after call-site updates
- [ ] `dotnet build` succeeds with zero errors
- [ ] `dotnet test` passes all existing tests

### Out of Scope

- Adding `RunToCompletion` methods (separate plan)
- Changing `CreateContext` signature
- Implementing tick-loop scheduler (`Runtime.tick()`)
- Making `Context.Run()` internal (left as open question in eval)

---

## Context

### Current State

`ZohRuntime.Run(Context ctx, CompiledStory story)` (lines 161–252 of `ZohRuntime.cs`) contains the full execution loop: instruction fetch, verb dispatch, IP advancement, continuation handling, and termination cleanup. The `story` parameter is only used for a null-guard:

```csharp
// Lines 165-168
if (ctx.CurrentStory == null)
{
    ctx.CurrentStory = story;
}
```

`Context` (lines 12–194 of `Context.cs`) has execution state (`InstructionPointer`, `CurrentStory`, `State`, `WaitCondition`) but no `Run()` method.

### Key Files

| File | Purpose | Changes Needed |
|------|---------|----------------|
| `c#/src/Zoh.Runtime/Execution/ZohRuntime.cs` | Runtime class with `Run()` loop | Remove `story` param; extract loop to delegate to `ctx.Run()` |
| `c#/src/Zoh.Runtime/Execution/Context.cs` | Execution context | Add `Run()` method containing the loop body |
| `c#/tests/Zoh.Tests/Execution/RuntimeTests.cs` | Runtime tests | Update `Run(ctx, story)` → `Run(ctx)` |
| `c#/tests/Zoh.Tests/Verbs/Standard/Media/*.cs` | Media verb tests (8 files) | Update `Run(ctx, story)` → `Run(ctx)` |
| `c#/tests/Zoh.Tests/Verbs/Standard/Presentation/*.cs` | Presentation verb tests (4 files) | Update `Run(ctx, story)` → `Run(ctx)` |

### Dependencies

- **Requires:** None — this is a self-contained refactor
- **Blocks:** `20260226-runtime-run-to-completion-plan.md`, `20260226-runtime-run-to-completion-shorthand-plan.md`

### Constraints

- Loop body in `Context.Run()` needs access to verb dispatch. The existing `VerbExecutor` delegate (`Func<ValueAst, IExecutionContext, VerbResult>`) is designed for `/do` verb execution (takes `ValueAst`, not `VerbCallAst`). The execution loop uses `VerbRegistry.GetDriver()` directly with `VerbCallAst` — a **different dispatch path**. A new delegate is needed for statement-level dispatch.

---

## Implementation

### Overview

1. Add a `StatementExecutor` delegate to `Context` for statement-level verb dispatch
2. Add a corresponding private method in `ZohRuntime` and wire it in `CreateContext`
3. Add a `Run()` method to `Context` containing the execution loop body
4. Slim `ZohRuntime.Run(ctx)` down to a thin shim that calls `ctx.Run()`
5. Remove the `story` parameter from `ZohRuntime.Run()`
6. Update all call sites in test files

### Step 1: Add Statement-Level Verb Dispatch Delegate to Context

**Objective:** Give `Context.Run()` the ability to resolve and execute verbs from `VerbCallAst` nodes without a direct reference to `VerbRegistry`.

> **IMPORTANT:** The existing `Context.ExecuteVerb(ValueAst, IExecutionContext)` and `VerbExecutor` delegate are for `/do` verb execution (verb-as-value references). They take `ValueAst`, NOT `VerbCallAst`. The execution loop in `ZohRuntime.Run()` uses a **different dispatch path**: it calls `VerbRegistry.GetDriver(call.Namespace, call.Name)` then `driver.Execute(ctx, call)` with a `VerbCallAst`. These are two separate paths and must remain so.

**Files:**
- `c#/src/Zoh.Runtime/Execution/Context.cs`
- `c#/src/Zoh.Runtime/Execution/ZohRuntime.cs`

**Changes in Context.cs:**

Add a new delegate property for statement-level dispatch (after the existing `VerbExecutor` on line 26):

```csharp
// After existing VerbExecutor property:
public Func<IExecutionContext, VerbCallAst, VerbResult>? StatementExecutor { get; set; }
```

**Changes in ZohRuntime.cs:**

Add a new private method that wraps the verb dispatch logic currently inlined in `Run()`:

```csharp
private VerbResult ExecuteStatement(IExecutionContext ctx, VerbCallAst call)
{
    var driver = VerbRegistry.GetDriver(call.Namespace, call.Name);
    if (driver != null)
    {
        try
        {
            return driver.Execute(ctx, call);
        }
        catch (ZohDiagnosticException ex)
        {
            return VerbResult.Fatal(new Diagnostic(ex.Severity, ex.DiagnosticCode, ex.Message, call.Start));
        }
        catch (Exception ex)
        {
            return VerbResult.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "runtime_error", $"Unhandled exception: {ex.Message}", call.Start));
        }
    }
    return VerbResult.Ok();
}
```

Wire it in `CreateContext()`:

```csharp
// Add this line in CreateContext(), after the existing delegate assignments:
ctx.StatementExecutor = ExecuteStatement;
```

**Rationale:** This keeps the existing `VerbExecutor` / `ExecuteVerb` path intact for `/do` verb usage. The new `StatementExecutor` delegate handles the statement-level dispatch that the execution loop needs.

**Verification:** `dotnet build` compiles.

### Step 2: Add `Run()` Method to Context

**Objective:** Move the execution loop body from `ZohRuntime.Run()` into `Context.Run()`.

**Files:**
- `c#/src/Zoh.Runtime/Execution/Context.cs`

**Changes:**

Add a `Run()` method after the existing `ExecuteVerb` method (after line 33):

```csharp
public void Run()
{
    while (State == ContextState.Running)
    {
        if (CurrentStory == null || InstructionPointer >= CurrentStory.Statements.Length)
        {
            Terminate();
            break;
        }

        var stmt = CurrentStory.Statements[InstructionPointer];

        // Capture state before execution to detect jumps
        int entryIp = InstructionPointer;
        CompiledStory entryStory = CurrentStory;

        if (stmt is StatementAst.VerbCall callStmt)
        {
            var call = callStmt.Call;
            var result = StatementExecutor!(this, call);

            if (!result.IsSuccess)
            {
                if (result.Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Fatal))
                {
                    LastDiagnostics = result.Diagnostics;
                    SetState(ContextState.Terminated);
                    break;
                }
            }
            LastResult = result.Value;
            LastDiagnostics = result.Diagnostics;

            if (result.Continuation != null)
            {
                Block(result.Continuation);
                break;
            }
        }
        else if (stmt is StatementAst.Label label)
        {
            var validation = ValidateContract(label.Name);
            if (!validation.IsSuccess)
            {
                if (validation.Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Fatal))
                {
                    LastDiagnostics = validation.Diagnostics;
                    SetState(ContextState.Terminated);
                    break;
                }
            }
        }

        // If still running and didn't jump, advance IP
        if (State == ContextState.Running &&
            InstructionPointer == entryIp &&
            CurrentStory == entryStory)
        {
            InstructionPointer++;
        }
    }
}
```

**Rationale:** Direct extraction of the loop from `ZohRuntime.Run()`. Uses `StatementExecutor!` delegate (non-null asserted — wired by `CreateContext`) for verb dispatch instead of directly calling `VerbRegistry.GetDriver()`.

**Verification:** `dotnet build` compiles; running any existing test exercises the loop through the updated path.

### Step 3: Slim Down `ZohRuntime.Run()` to a Thin Shim

**Objective:** Replace the loop body in `ZohRuntime.Run()` with a delegation to `ctx.Run()` and remove the `story` parameter.

**Files:**
- `c#/src/Zoh.Runtime/Execution/ZohRuntime.cs`

**Changes:**

```csharp
// Before:
public void Run(Context ctx, CompiledStory story)
{
    // ... 90 lines of loop body ...
}

// After:
public void Run(Context ctx)
{
    ctx.Run();
}
```

**Rationale:** The spec says `run(context: Context): void` — no story param. The runtime delegates to the context's own loop.

**Verification:** `dotnet build` compiles; all existing logic is preserved, just relocated.

### Step 4: Update All Test Call Sites

**Objective:** Mechanically update all `runtime.Run(ctx, story)` calls to `runtime.Run(ctx)`.

**Files:** All 13 test files listed in Key Files.

**Changes:**

```csharp
// Before (every test file):
runtime.Run(ctx, story);

// After:
runtime.Run(ctx);
```

This is a bulk find-and-replace. The compiler will catch any missed sites.

**Rationale:** Mechanical rename; static typing guarantees correctness.

**Verification:** `dotnet build` with no errors confirms all sites updated. `dotnet test` confirms no regressions.

---

## Verification Plan

### Automated Checks

- [ ] `dotnet build` in `c#/` — zero errors
- [ ] `dotnet test` in `c#/` — all existing tests pass
- [ ] No remaining references to `Run(ctx, story)` in codebase (grep check)

### Manual Verification

- [ ] Confirm `Context.Run()` is public
- [ ] Confirm `ZohRuntime.Run()` signature has only one parameter

### Acceptance Criteria Validation

| Criterion | How to Verify | Expected Result |
|-----------|---------------|-----------------|
| `Run(Context ctx)` signature | Inspect `ZohRuntime.cs` | Single `Context` param, no `CompiledStory` |
| Loop in `Context.Run()` | Inspect `Context.cs` | Full loop body present |
| Runtime is thin shim | Inspect `ZohRuntime.Run()` | Body is `ctx.Run()` only |
| All tests pass | `dotnet test` | 0 failures |
| No stale references | `grep -r "Run(ctx, story\|Run(context, story"` | 0 matches |

---

## Rollback Plan

1. `git revert` the commit — all changes are in a single refactor commit
2. The change is purely structural (no logic change), so rollback is clean

---

## Notes

### Assumptions

- `StatementExecutor` delegate → `ZohRuntime.ExecuteStatement()` handles all verb dispatch (exception wrapping, driver resolution, null driver) identically to the current inlined logic in `ZohRuntime.Run()`
- The existing `VerbExecutor` / `ExecuteVerb(ValueAst, IExecutionContext)` path is NOT affected — it remains for `/do` verb execution
- No external code (outside the `c#/` project) calls `ZohRuntime.Run()` directly
- The `using` directives in `Context.cs` already include the necessary types (`StatementAst`, `DiagnosticSeverity`, etc.) or will need additions

### Risks

- **Missing `using` directives in Context.cs:** The loop references `StatementAst`, `DiagnosticSeverity`, etc. — may need new `using` statements. **Mitigation:** compiler will flag immediately.
- **`StatementExecutor` null:** If `CreateContext` is bypassed or `StatementExecutor` not wired, the null-forgiving `!` in `Context.Run()` will throw. **Mitigation:** `CreateContext` is the only factory; wiring is guaranteed.

