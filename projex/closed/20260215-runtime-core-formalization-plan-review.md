# Review: Runtime Core Formalization

> **Review Date:** 2026-02-15
> **Reviewer:** Agent
> **Reviewed Projex:** [20260214-runtime-core-formalization-plan.md](20260214-runtime-core-formalization-plan.md)
> **Original Date:** 2026-02-14
> **Time Since Creation:** 1 day

---

## Review Summary

**Verdict:** Valid

The plan accurately identifies architectural limitations in the current `ZohRuntime` implementation (monolithic pipeline, lack of extensibility points) and proposes a structured solution that aligns with the Phase 4 roadmap. The technical approach—introducing `HandlerRegistry`, `RuntimeConfig`, and priority-based handler execution—is sound and necessary for upcoming features (storage, validation, standard verbs).

---

## Timeline Analysis

### When Authored
- Created: 2026-02-14
- Status: Draft targeting Phase 4.1.

### What Changed Since
- **Context:** The codebase state matches the plan's description exactly. `ZohRuntime.cs` is still the monolithic version described.
- **Dependencies:** No new conflicting dependencies introduced.

---

## Status Quo Assessment

### Current State
`ZohRuntime` currently hardcodes the `Lex` -> `Parse` -> `Validate` -> `Compile` pipeline within `LoadStory`. Extensibility is limited as preprocessors are defined but not wired up, and verb drivers lack priority metadata for conflict resolution.

### Validity of Assertions
| Assertion | Verification | Notes |
|-----------|--------------|-------|
| `ZohRuntime.LoadStory()` is monolithic | Valid | Verified in `ZohRuntime.cs` |
| `IVerbDriver` lacks `Priority` | Valid | Verified in `IVerbDriver.cs` |
| `NamespaceValidator` hardcoded to AST | Valid | Verified in `NamespaceValidator.cs` |
| Preprocessors exist but unused | Valid | `IPreprocessor` exists but `ZohRuntime` doesn't use it |

---

## Validity Assessment

### Problems Stated
The identified problems (lack of config, rigid pipeline, no priority system) are valid blockers for the extensible architecture required by the spec (`impl/09_runtime.md`).

### Approach Proposed
The `HandlerRegistry` pattern with ordered lists is a standard and effective way to handle this extensibility.
- **Refactoring Strategy:** Incremental refactoring (keeping `NamespaceValidator` hardcoded ensuring compiled story wrapper stability) is a pragmatic approach to avoid "boiling the ocean" in one step.
- **Language Features:** Default interface implementation for `IVerbDriver.Priority` ensures backward compatibility. Verified project targets `.net8.0`, so this feature is available.

---

## Accuracy Assessment

### Technical Content
- Code references are accurate.
- Proposed new interfaces (`IStoryValidator`, `IVerbValidator`) match the spec's intent.

---

## Challenge Questions

### Challenge 1: Is `NamespaceValidator` being left behind?
The plan explicitly notes `NamespaceValidator` will remain hardcoded in `LoadStory` for now.
**Risk:** We might create a "legacy" path vs "new pipeline" path divergence.
**Assessment:** Acceptable. `NamespaceValidator` currently operates on `StoryAst`. Converting it to `IStoryValidator` (which operates on `CompiledStory`) requires either mapped access or a refactor of the validator. Deferring this to Milestone 4.3 (Validation) keeps this change focused on *infrastructure* rather than *logic migration*.

### Challenge 2: Is `Priority` on `IVerbDriver` breaking?
Adding a property to an interface is a breaking change for implementors.
**Mitigation:** The plan proposes a default implementation (`int Priority => 0;`).
**Assessment:** Verified supported by `.net8.0`. Safe.

---

## Value Assessment

**Value Verdict:** High.
This plan acts as the foundational work for all subsequent features in this phase. Without it, adding features like efficient storage, robust validation, or standard verb libraries would clutter `ZohRuntime` further.

---

## Recommendations

### Required Changes
None. The plan is technically sound and ready for execution.

### Action Items
- [ ] Approve plan and proceed to execution.

