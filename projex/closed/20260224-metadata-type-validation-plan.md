---
description: Plan for fixing metadata type validation gaps
---

# Metadata Type Validation Fix

> **Status:** Complete
> **Created:** 2026-02-24
> **Completed:** 2026-02-24
> **Walkthrough:** [20260224-metadata-type-validation-walkthrough.md](20260224-metadata-type-validation-walkthrough.md)
> **Author:** Antigravity
> **Source:** Direct request from `plan-projex for fix` on `20260223-csharp-spec-audit-nav.md`
> **Related Projex:** `20260223-csharp-spec-audit-nav.md`

---

## Summary

This plan addresses a compliance gap where the C# runtime fails to strictly validate Story Metadata types. The ZOH spec restricts metadata to `boolean`, `integer`, `double`, `string`, `list`, and `map`. Currently, the AST-to-CompiledStory pipeline in `CompiledStory.FromAst` accepts unsupported types like `nothing` and `verb`, and throws an unhandled `NotImplementedException` for expressions, references, and channels. We will add explicit AST validation and robust diagnostic reporting during compilation. 

**Scope:** `CompiledStory` AST conversion logic, `ZohRuntime` compilation pipeline, and related tests.
**Estimated Changes:** 3 files

---

## Objective

### Problem / Gap / Need
During `CompiledStory.FromAst`, the conversion iterates over the `StoryAst.Metadata` dictionary and calls `ValueResolver.ResolveContextless(kvp.Value)`. 
1. `ValueResolver.ResolveContextless` permits `ValueAst.Nothing` and `ValueAst.Verb`, which violates the spec.
2. `ValueResolver.ResolveContextless` throws a hard `NotImplementedException` for `Reference`, `Expression`, and `Channel`, crashing the compiler instead of exposing a proper `Diagnostic`.

### Success Criteria
- [ ] ZOH scripts with invalid metadata types (e.g. `meta: ?;`, `meta: /verb;;`, `meta: *ref;`) generate an `invalid_metadata_type` compiler error diagnostic instead of crashing.
- [ ] Valid metadata types (`boolean`, `integer`, `double`, `string`, `list`, `map`) correctly parse and compile.
- [ ] Nested invalid types (e.g. a list containing a reference) are also caught and reported.

### Out of Scope
- Modifying standard execution-time `ValueResolver.Resolve`.
- Changes to parser syntax for metadata (parser remains flexible; AST validation handles semantics).

---

## Context

### Current State
`ZohRuntime.LoadStory` orchestrates compilation. It calls `var compiled = CompiledStory.FromAst(parseResult.Story!);`. The `CompiledStory.FromAst` method lacks access to a `DiagnosticBag` and assumes all AST values can be safely converted via `ValueResolver.ResolveContextless`. 

### Key Files
| File | Purpose | Changes Needed |
|------|---------|----------------|
| `src/Zoh.Runtime/Execution/CompiledStory.cs` | Converts `StoryAst` to runtime format | Inject `DiagnosticBag`. Add recursive Type validation before resolving. |
| `src/Zoh.Runtime/Execution/ZohRuntime.cs` | Orchestrates the compiler pipeline | Pass the active `DiagnosticBag` into `CompiledStory.FromAst`. Check for errors. |
| `tests/Zoh.Tests/Execution/ZohRuntimeTests.cs` | Tests runtime behavior / compilation | Add test cases asserting compilation format errors for invalid metadata types. |

### Dependencies
- **Requires:** None
- **Blocks:** Completing item 1.2 "Story Structure" in the spec compliance audit.

---

## Implementation

### Overview
We will update `CompiledStory.FromAst` to receive a `DiagnosticBag`. A new validation helper `IsValidMetadataValue` will recursively inspect the `ValueAst`. If invalid, `FromAst` will report an `invalid_metadata_type` error directly to the `DiagnosticBag` and exclude the offending key from the compiled metadata dictionary. `ZohRuntime.LoadStory` will pass its active bag to `FromAst` and short-circuit if fatal errors occurred during compilation.

### Step 1: Update `CompiledStory.FromAst` signature and validation logic

**Objective:** Plumb `DiagnosticBag` and introduce explicit type checking.

**Files:**
- `s:\repos\zoh\c#\src\Zoh.Runtime\Execution\CompiledStory.cs`

**Changes:**

```csharp
// Before:
public static CompiledStory FromAst(StoryAst ast)
{
    // ...
    foreach (var kvp in ast.Metadata)
    {
        b.Add(kvp.Key, ValueResolver.ResolveContextless(kvp.Value));
    }
    // ...
}

// After:
using Zoh.Runtime.Diagnostics;

// ...
public static CompiledStory FromAst(StoryAst ast, DiagnosticBag diagnostics)
{
    // ... maps ...
    
    var b = ImmutableDictionary.CreateBuilder<string, ZohValue>();
    foreach (var kvp in ast.Metadata)
    {
        if (IsValidMetadataAst(kvp.Value))
        {
            try 
            {
                b.Add(kvp.Key, ValueResolver.ResolveContextless(kvp.Value));
            }
            catch(Exception ex) 
            {
                diagnostics.ReportError("invalid_metadata_type", $"Failed to resolve metadata '{kvp.Key}': {ex.Message}", kvp.Value.Position);
            }
        }
        else
        {
            diagnostics.ReportError("invalid_metadata_type", $"Metadata value for '{kvp.Key}' has unsupported type. Allowed types are boolean, integer, double, string, list, and map.", kvp.Value.Position);
        }
    }
    // ...
}

private static bool IsValidMetadataAst(ValueAst ast)
{
    return ast switch
    {
        ValueAst.Boolean or ValueAst.Integer or ValueAst.Double or ValueAst.String => true,
        ValueAst.List l => l.Elements.All(IsValidMetadataAst),
        ValueAst.Map m => m.Entries.All(e => IsValidMetadataAst(e.Key) && IsValidMetadataAst(e.Value)),
        _ => false
    };
}
```

**Rationale:** AST traversal is much safer than relying on `ResolveContextless` to throw. This strictly enforces the basic ZOH types allowed for metadata.

---

### Step 2: Inject DiagnosticBag from ZohRuntime

**Objective:** Pass the compiler diagnostic bag to `FromAst` and guard progression.

**Files:**
- `s:\repos\zoh\c#\src\Zoh.Runtime\Execution\ZohRuntime.cs`

**Changes:**

```csharp
// Before:
        // 4. Compile (currently: wrap AST)
        var compiled = CompiledStory.FromAst(parseResult.Story!);

// After:
        // 4. Compile (currently: wrap AST)
        var compiled = CompiledStory.FromAst(parseResult.Story!, diagnostics);
        if (diagnostics.HasErrors)
            throw new CompilationException("Compilation failed", diagnostics);
```

**Rationale:** The orchestrator needs to catch and abort if metadata is fundamentally un-compilable.

---

### Step 3: Add unit tests

**Objective:** Validate that bad metadata triggers clear errors rather than exceptions.

**Files:**
- `s:\repos\zoh\c#\tests\Zoh.Tests\Execution\ZohRuntimeTests.cs` (or `CompiledStoryTests.cs` if it exists, otherwise create new file `Zoh.Tests/Execution/CompiledStoryTests.cs` or test via `ZohRuntime`).

**Changes:**
Create new tests:
- `LoadStory_WithInvalidMetadataTypeVerb_ThrowsCompilationException`
- `LoadStory_WithInvalidMetadataTypeReference_ThrowsCompilationException`
- `LoadStory_WithInvalidMetadataTypeNothing_ThrowsCompilationException`
Assert that `CompilationException` is thrown, catching `invalid_metadata_type` in the `Diagnostics` bag.

---

## Verification Plan

### Automated Checks
- [ ] Run `dotnet build` in `s:\repos\zoh\c#`
- [ ] Run tests with `dotnet test --filter "Zoh.Tests.Execution"` in `s:\repos\zoh\c#`

### Acceptance Criteria Validation
| Criterion | How to Verify | Expected Result |
|-----------|---------------|-----------------|
| Invalid metadata types generate compiler diagnostic | Parse `my_meta: /verb;;` with `ZohRuntime.LoadStory` | Throws `CompilationException`, Diagnostic bag contains `invalid_metadata_type`. |
| Unsupported AST nodes don't crash parser | Parse `my_meta: *ref;` | Throws `CompilationException` instead of `NotImplementedException`. |
| Valid metadata passes compilation | Parse `my_meta: "string";` | Compiles successfully without diagnostics. |

---

## Notes

### Assumptions
- `ZohRuntime.LoadStory()` handles `CompilationException` properly and exposes the `Diagnostics` outward.
- We do not consider empty list/map invalid, as they resolve correctly to empty collections.

### Risks
- If any existing tests rely on parser flexibility to store `nothing` via `key: ?;` in metadata, those tests will fail. Mitigation: update those generic tests to strictly use valid metadata scalar types.

### Open Questions
- [ ] None.
