# Validation Pipeline Implementation Plan

> **Status:** Complete
> **Completed:** 2026-02-16
> **Walkthrough:** [20260216-validation-pipeline-walkthrough.md](./20260216-validation-pipeline-walkthrough.md)
> **Author:** Antigravity (Agent)
> **Source:** Direct request from `c#/projex/20260207-csharp-runtime-nav.md`
> **Related Projex:** `c#/projex/20260207-csharp-runtime-nav.md`

---

## Summary

This plan outlines the implementation of the Validation Pipeline for the C# ZOH runtime. It introduces formal validators for stories and verbs, integrates them into the `HandlerRegistry`, and standardizes diagnostic reporting using the new `Diagnostic` infrastructure. This ensures scripts are rigorously checked before execution, catching errors like duplicate labels, missing parameters, and invalid types early.

**Scope:** C# Runtime (`Zoh.Runtime` project).
**Estimated Changes:** ~10-15 files (new validators, updated registry, modified runtime).

---

## Objective

### Problem / Gap / Need
Currently, validation is minimal and ad-hoc (`NamespaceValidator`). The runtime lacks:
1.  **Comprehensive Story Validation:** Checking for duplicate labels across the story, identifying jump targets that don't exist, and verifying required verbs.
2.  **Verb-Specific Validation:** Checking parameter counts and types for core verbs at compile/load time where possible.
3.  **Unified Diagnostic Pipeline:** A consistent way to collect and report errors from all stages (lexing to runtime).
4.  **Extensibility:** A way to register custom validators.

### Success Criteria
- [ ] `LabelValidator` implemented and registered (catches duplicate labels).
- [ ] `JumpTargetValidator` implemented and registered (warns on unknown labels).
- [ ] `RequiredVerbsValidator` implemented and registered.
- [ ] `SetValidator`, `JumpValidator`, and standard verb validators `IVerbValidator`s implemented.
- [ ] `NamespaceValidator` refactored into `VerbResolutionValidator` which uses `HandlerRegistry` to delegate to specific `IVerbValidator`s.
- [ ] `ZohRuntime` pipeline updated to use the new validators via `HandlerRegistry`.
- [ ] All 515 existing tests pass.
- [ ] New tests added for validation scenarios.

### Out of Scope
-   Runtime compilation of expressions (deferred to future phases).
-   Implementation of Standard Verbs (Presentation/Media) - these are Phase 4.4/4.5.

---

## Context

### Current State
-   `NamespaceValidator.cs`: Hardcoded validator that checks if verbs exist. Uses custom `ValidationError`.
-   `Diagnostics/`: `Diagnostic` classes exist.
-   `HandlerRegistry.cs`: Has `StoryValidators` and `VerbValidators` collections.
-   `IStoryValidator` and `IVerbValidator` interfaces exist.

### Dependencies
-   **Requires:** `HandlerRegistry` (Phase 4.1 - Done).

---

## Implementation

### Step 1: Implement Story Validators

**Objective:** Implement the three core story validators defined in the spec.

**Files:**
-   `src/Zoh.Runtime/Validation/LabelValidator.cs` (New)
-   `src/Zoh.Runtime/Validation/RequiredVerbsValidator.cs` (New)
-   `src/Zoh.Runtime/Validation/JumpTargetValidator.cs` (New)

**Changes:**
-   **LabelValidator:** Iterate `story.Labels`. Check for duplicates (case-insensitive). Return Fatal diagnostics.
-   **RequiredVerbsValidator:** Check `story.Metadata["required_verbs"]`. Check against `HandlerRegistry.VerbDrivers`. Return Fatal diagnostics if missing.
-   **JumpTargetValidator:** Iterate statements. If `Jump`/`Fork`/`Call` with local target (literal string), check if label exists in `story.Labels`. Return Warning diagnostics.

### Step 2: Implement Verb Validators

**Objective:** Implement specific validators for core verbs.

**Files:**
-   `src/Zoh.Runtime/Validation/CoreVerbs/SetValidator.cs` (New)
-   `src/Zoh.Runtime/Validation/CoreVerbs/JumpValidator.cs` (New)
-   `src/Zoh.Runtime/Validation/CoreVerbs/FlowValidators.cs` (New - Fork, Call)

**Changes:**
-   **SetValidator:** Implement `IVerbValidator`. Check `Set` verb. Enforce 2+ args. First arg must be Reference or String. Check `[typed]` attribute against allowed types.
-   **JumpValidator:** Check `Jump`. Enforce 2 args (story, label).
-   **FlowValidators:** Similar for `Fork` and `Call`.

### Step 3: Refactor Verb Resolution Validator

**Objective:** Replace `NamespaceValidator` with a proper `IStoryValidator` that delegates to `IVerbValidator`s.

**Files:**
-   `src/Zoh.Runtime/Validation/VerbResolutionValidator.cs` (New)
-   `src/Zoh.Runtime/Validation/NamespaceValidator.cs` (Delete)

**Changes:**
-   Create `VerbResolutionValidator`.
-   Iterate all `VerbCall`s in story.
-   Check if verb exists in `HandlerRegistry.VerbDrivers`. If not, error.
-   Look up `IVerbValidator`s for the verb in `HandlerRegistry.VerbValidators`.
-   Run all matching validators and aggregate diagnostics.
-   This consolidates "Does verb exist?" and "Is verb usage valid?" into one pass.

### Step 4: Integration

**Objective:** Wire everything into `ZohRuntime` and `HandlerRegistry`.

**Files:**
-   `src/Zoh.Runtime/Execution/ZohRuntime.cs`
-   `src/Zoh.Runtime/Execution/HandlerRegistry.cs`

**Changes:**
-   `HandlerRegistry`: Register `LabelValidator`, `RequiredVerbsValidator`, `JumpTargetValidator`, and `VerbResolutionValidator` as Story Validators.
-   `HandlerRegistry`: Register `SetValidator`, `JumpValidator`, etc. as Verb Validators.
-   `ZohRuntime`: Remove instantiation of `NamespaceValidator`. Trust `Handlers.StoryValidators` to do the work.

---

## Verification Plan

### Automated Checks
-   **Unit Tests:**
    -   `tests/Zoh.Runtime.Tests/Validation/StoryValidatorTests.cs`: Test duplicate labels, missing required verbs.
    -   `tests/Zoh.Runtime.Tests/Validation/VerbValidatorTests.cs`: Test `set` with invalid args, `jump` with invalid targets.
    -   `tests/Zoh.Runtime.Tests/Validation/IntegrationTests.cs`: Load a story with multiple errors, verify diagnostics.

### Manual Verification
-   Create `validation_test.zoh`:
    ```zoh
    Test Story
    required_verbs: ["/missing_verb"];
    ===
    @duplicate_label
    @duplicate_label
    /set; // Missing args
    /jump ?, "nowhere"; // Unknown label
    ```
-   Run via `ZohRuntime.LoadStory`. Verify diagnostics contain:
    -   FATAL: Missing required verb
    -   FATAL: Duplicate label
    -   FATAL: Set missing parameter
    -   WARNING: Jump to unknown label

---

## Rollback Plan
-   Revert `ZohRuntime.cs` to use `NamespaceValidator`.
-   Delete new validator files.
