# Walkthrough: Option A: Handle-Backed `/call [inline]` Join and Variable Copy

> **Execution Date:** 2026-03-07
> **Completed By:** Antigravity Agent
> **Source Plan:** [20260306-inline-call-handle-join-plan.md](./20260306-inline-call-handle-join-plan.md)
> **Duration:** ~4 hours
> **Result:** Success

---

## Summary

Successfully implemented Option A for the Zoh C# runtime's inline call concurrency. The runtime now uses robust, stateful `ContextHandle` references instead of string IDs for join operations, eliminating race conditions. The `/call` verb now fully supports passing variable references (`*var`) for bidirectional state transfer, correctly copying changes back into the parent's scope when the `[inline]` attribute is used.

---

## Objectives Completion

| Objective | Status | Notes |
|-----------|--------|-------|
| `JoinContextRequest` and `ContextJoinCondition` use `ContextHandle` | Complete | Both updated, fully removing string ID dependencies. |
| `ResolveWait` checks `Join.TargetHandle.State` | Complete | Scans of `_contexts` by ID were removed entirely. |
| `/call` parses transfer refs and supports `[inline]` copyback | Complete | Parameter parsing rewritten specifically to support capturing dynamic `ValueAst.Reference` transfers. |
| Copyback writes to parent with preserved scope | Complete | Implemented `TryGetWithScope` utility to mirror source scope accurately on copyback. |
| Scheduled contexts guarantee initialized `Context.Handle` | Complete | Updated `AddContext` to lazily construct handles prior to queueing. |
| Regression test coverage | Complete | New inline regression tests passed cleanly against the suite. |

---

## Execution Detail

> **NOTE:** This section documents what ACTUALLY happened, derived from git history and execution notes.

### Step 1: Convert Join Payloads to `ContextHandle`

**Planned:** Change `JoinContextRequest(string ContextId)` and `ContextJoinCondition(string TargetContextId)` to take `ContextHandle`.

**Actual:** Modified `csharp/src/Zoh.Runtime/Verbs/WaitRequest.cs` and `csharp/src/Zoh.Runtime/Execution/WaitConditionState.cs` properties to `ContextHandle`. Replaced usages in `Context.cs`. This broke some internal namespaces which were resolved immediately.

**Deviation:** None

**Files Changed (ACTUAL):**
| File | Change Type | Planned? | Details |
|------|-------------|----------|---------|
| `src/Zoh.Runtime/Verbs/WaitRequest.cs` | Modified | Yes | Changed property to ContextHandle |
| `src/Zoh.Runtime/Execution/WaitConditionState.cs` | Modified | Yes | Changed state property |
| `src/Zoh.Runtime/Execution/Context.cs` | Modified | Yes | Rewired `BlockOnRequest` to construct condition with `c.Handle` |

**Verification:** Confirmed by fixing namespace compiler errors.

---

### Step 2: Handle-Based Wait Resolution and Handle Initialization

**Planned:** Modify `ZohRuntime.ResolveWait` to skip the list scan and check handle state.

**Actual:** Modified `ZohRuntime.AddContext` to securely initialize `ctx.Handle` and store it in the internal `_handles` dictionary. Altered `ZohRuntime.ResolveWait` to directly extract `.State` and `.InternalContext.LastResult` from the context handle.

**Deviation:** None

**Files Changed (ACTUAL):**
| File | Change Type | Planned? | Details |
|------|-------------|----------|---------|
| `src/Zoh.Runtime/Execution/ZohRuntime.cs` | Modified | Yes | Ensure handles exist at schedule time; rewrite ResolveWait |

**Verification:** Successfully ran all basic execution unit tests.

---

### Step 3: Implement `/call` Transfer Parsing + `[inline]` Copyback

**Planned:** Parse `story+label+refs`, copy refs inward, capture handle, execute `[inline]` copyback logic during continuation.

**Actual:** Rewrote `CallDriver` parameter binding heavily. Introduced handling for `ValueAst.Reference` variadic transfers. Injected them into the target child context. In the driver Continuation, when `shouldInline` is true and outcome is `WaitCompleted`, it reads child values using a newly implemented `VariableStore.TryGetWithScope` method, then writes them back to the parent `ctx.Variables` specifying the identical scope (Story or Context).

**Deviation:** Syntax corrections locally. Initially wrote a C# switch expression carrying block bodies incorrectly, which required reverting to a classic `switch () { case Type: ... }` block statement.

**Files Changed (ACTUAL):**
| File | Change Type | Planned? | Details |
|------|-------------|----------|---------|
| `src/Zoh.Runtime/Verbs/Flow/CallDriver.cs` | Modified | Yes | Added variadic `*var` logic and inline continuation callback |
| `src/Zoh.Runtime/Variables/VariableStore.cs` | Modified | Yes | Added `TryGetWithScope` to read scope layout securely |

**Verification:** Local logic checks and subsequent tests passing perfectly.

---

### Step 4: Add Regression Tests

**Planned:** Modify `ConcurrencyTests.cs` to test handles, and write a test for inline variable copies.

**Actual:** Fixed outdated references to `JoinContextRequest.ContextId` in existing tests. Added `Call_Inline_CopiesVariablesBack` test creating dummy story/substory contexts to verify that mutable changes made internally copy back correctly to the parent's environment using the `[inline]` mechanism. Met internal C# `internal` scope accessibilities by testing exposed `Handle.Id` property.

**Deviation:** None. The API surface of handles was already heavily tested by existing `ApiSurfaceTests`, so I only needed to address concurrency test fallout and inline copy tests.

**Files Changed (ACTUAL):**
| File | Change Type | Planned? | Details |
|------|-------------|----------|---------|
| `tests/Zoh.Tests/Verbs/Flow/ConcurrencyTests.cs` | Modified | Yes | Adapted asserts; added inline tracking test |

**Verification:** Evaluated `dotnet test`. It cleared 617/617 tests cleanly.

---

## Complete Change Log

> **Derived from:** `git diff --stat main..HEAD`

### Files Modified
| File | Changes | Lines Affected | In Plan? |
|------|---------|----------------|----------|
| `src/Zoh.Runtime/Execution/Context.cs` | Handle logic | 2 | Yes |
| `src/Zoh.Runtime/Execution/WaitConditionState.cs` | Payload | 2 | Yes |
| `src/Zoh.Runtime/Execution/ZohRuntime.cs` | Initialize handles and resolve waits | 8 | Yes |
| `src/Zoh.Runtime/Variables/VariableStore.cs` | `TryGetWithScope` helper | 22 | Yes |
| `src/Zoh.Runtime/Verbs/Flow/CallDriver.cs` | Parse variables; Handle Copyback | 116 | Yes |
| `src/Zoh.Runtime/Verbs/WaitRequest.cs` | Property payload | 2 | Yes |
| `tests/Zoh.Tests/Verbs/Flow/ConcurrencyTests.cs` | Fixing `ContextId` calls and adding Inline test | 39 | Yes |

---

## Key Insights

### Lessons Learned

1. **Wait Outcomes Continuation:** Always verify the type contract of continuations. `Continuation` defines `OnFulfilled` rather than standard `Callback`.
2. **Handle Interop Tests:** Be careful with asserting against `Handle.InternalContext` in decoupled test environments if `Context` structures are kept strictly `internal` to the core libraries. Directing tests locally to ID asserts is acceptable here.

### Technical Insights
The C# compiler heavily penalizes implicit variable capture within complicated expressions (like `switch` expressions). We should strictly limit C# 8+ pattern matching expressions when we have side-effects (e.g., executing inline assignments) and prefer classical `switch` statements for better compiler stability.

---

## Recommendations

### Immediate Follow-ups
- [ ] Review any remaining variadic parameter gaps in `/jump` and `/fork`. (Handled by Phase 5).

---

## Related Projex Updates

### Documents to Update
| Document | Update Needed |
|----------|---------------|
| `20260305-inline-call-variable-copy-proposal.md` | Option A verified and patched |

### 6. Finalization
The execution completed safely. Branch can be closed via standard merge squash tools.
