# Plan: C# Parse Verb Whitespace Trimming

> **Status:** Complete
> **Created:** 2026-02-08
> **Author:** Agent
> **Source:** [Plan: Parse Verb Whitespace Trimming](../projex/20260208-parse-whitespace-trimming-plan.md)
> **Related Projex:** [Plan: Parse Verb Whitespace Trimming](../projex/20260208-parse-whitespace-trimming-plan.md)

---

## Summary

This plan implements the whitespace trimming rule for the `/parse` verb in the C# runtime, ensuring compliance with the updated specification. It involves modifying `ParseDriver.cs` and adding comprehensive tests.

**Scope:** C# Runtime (`ParseDriver.cs`, Tests).
**Estimated Changes:** 2 files.

---

## Objective

### Problem / Gap / Need
The C# runtime currently relies on `long.TryParse` behavior for numbers (which allows whitespace) but manually handles whitespace for List/Map detection. This behavior is implicit and potentially inconsistent. The spec now mandates explicit trimming.

### Success Criteria
- [ ] `ParseDriver.cs` explicitly trims input before processing.
- [ ] New unit tests verify that `"  123  "` parses as `123` (integer), `"  true  "` as `true` (boolean), etc.
- [ ] List/Map detection logic remains correct after pre-trimming.

### Out of Scope
- Documentation changes (covered in parent plan).

---

## Context

### Current State
- `ParseDriver` uses `long.TryParse`, `double.TryParse` (implicit whitespace handling).
- `InferType` does `str.TrimStart().StartsWith(...)` for Lists/Maps.

### Key Files
| File | Purpose | Changes Needed |
|------|---------|----------------|
| `c#/src/Zoh.Runtime/Verbs/Core/ParseDriver.cs` | Implementation | Add `.Trim()` to input. |
| `c#/tests/Zoh.Tests/Verbs/Core/ParseTests.cs` | Verification | Add whitespace test cases. |

### Dependencies
- **Requires:** None (Spec update is parallel).

---

## Implementation

### Step 1: Update C# Runtime
**Objective:** Enforce trimming in `ParseDriver`.

**Files:**
- `s:\repos\zoh\c#\src\Zoh.Runtime\Verbs\Core\ParseDriver.cs`

**Changes:**
```csharp
// In ParseDriver.Execute:
string str = value.AsString().Value.Trim(); 
// Remove redundant TrimStart() in InferType if no longer needed, 
// or verify it operations on the already trimmed string.
```

### Step 2: Add Tests
**Objective:** Verify whitespace handling.

**Files:**
- `s:\repos\zoh\c#\tests\Zoh.Tests\Verbs\Core\ParseTests.cs` (create if missing)

**Changes:**
Add tests covering:
- Integer with padding: `"  42  "` -> `42`
- Double with padding: `"  3.14  "` -> `3.14`
- Boolean with padding: `"  true  "` -> `true`
- List detection with padding: `"  [1]  "` -> List
- Map detection with padding: `"  {a:1}  "` -> Map

---

## Verification Plan

### Automated Checks
- [ ] Run C# tests: `dotnet test --filter "ParseTests"`

### Acceptance Criteria Validation
| Criterion | How to Verify | Expected Result |
|-----------|---------------|-----------------|
| Runtime Behavior | Run `ParseTests` | All pass |
