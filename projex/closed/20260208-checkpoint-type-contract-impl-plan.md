# Implement Checkpoint Type Contracts in C#

> **Status:** Complete
> **Created:** 2026-02-08
> **Completed:** 2026-02-08
> **Author:** Antigravity (on behalf of User)
> **Source:** [Proposal](file:///s:/repos/zoh/projex/20260208-checkpoint-type-contract-proposal.md)
> **Walkthrough:** [Walkthrough](file:///s:/repos/zoh/c%23/projex/closed/20260208-checkpoint-type-contract-walkthrough.md)
> **Related Projex:** [Spec Plan](file:///s:/repos/zoh/projex/20260208-checkpoint-type-contract-spec-plan.md)

---

## Summary

This plan implements checkpoint type contracts in the C# runtime. It involves updating the AST and Parser to support `@checkpoint *var:type` syntax, storing these contracts in the compiled story, and enforcing them during navigation (`Jump`, `Call`, `Fork`).

**Scope:** `c#` folder.
**Estimated Changes:** 6 files.

---

## Objective

### Problem / Gap / Need
The C# runtime currently parses checkpoints as simple labels, ignoring any contract variables. To support the new type contract feature (and contracts in general), the runtime must parse, store, and validate these contracts.

### Success Criteria
- [ ] `Parser` correctly parses `@check *var` and `@check *var:type`.
- [ ] `StoryAst` and `CompiledStory` store checkpoint contracts.
- [ ] `/jump`, `/call`, and `/fork` instructions validate existence and types of contract variables.
- [ ] Execution loop (`ZohRuntime.Run`) validates checkpoints when execution falls through to them.
- [ ] `checkpoint_violation` fatal error is raised on failure in both cases.

### Out of Scope
- Spec updates (covered by separate plan).

---

## Context

### Current State
- `Parser.cs`: `ParseLabel` only parses `@name`.
- `StatementAst.cs`: `Label` only stores `Name`.
- `CompiledStory.cs`: Stores labels as `string -> int` mapping.
- `NavigationDrivers`: Only resolve label index; do not validate state.

### Key Files
| File | Purpose | Changes Needed |
|------|---------|----------------|
| `src/Zoh.Runtime/Parsing/Ast/StatementAst.cs` | AST Nodes | Update `Label` to include `ImmutableArray<ContractParam>`. |
| `src/Zoh.Runtime/Parsing/Parser.cs` | Parsing logic | Update `ParseLabel` to parse optional contract params. |
| `src/Zoh.Runtime/Execution/CompiledStory.cs` | Runtime Model | Add `Contracts` dictionary. |
| `src/Zoh.Runtime/Verbs/Flow/JumpDriver.cs` | Runtime Logic | Add validation logic. |
| `src/Zoh.Runtime/Verbs/Flow/CallDriver.cs` | Runtime Logic | Add validation logic. |
| `src/Zoh.Runtime/Verbs/Flow/ForkDriver.cs` | Runtime Logic | Add validation logic. |
| `src/Zoh.Runtime/Execution/ZohRuntime.cs` | Main Loop | Update `Run` loop to validate checkpoints on fall-through. |

### Dependencies
- **Requires:** [Spec Plan](file:///s:/repos/zoh/projex/20260208-checkpoint-type-contract-spec-plan.md) (Conceptually)

---

## Implementation

### Overview
1.  Define `ContractParam` record (Name, Type name).
2.  Update AST to hold list of params.
3.  Update Parser to consume `*identifier` and optional `:type`.
4.  Update CompiledStory to map `checkpoint_name -> list<ContractParam>`.
5.  Implement validation helper method.
5.  Implement validation helper method.
6.  Call validation in navigation drivers.
7.  Call validation in `ZohRuntime.Run` loop.

### Step 1: Update AST and Parser

**Objective:** Parse `@checkpoint *var:type`.

**Files:**
- `src/Zoh.Runtime/Parsing/Ast/StatementAst.cs`
- `src/Zoh.Runtime/Parsing/Parser.cs`

**Changes:**
- Define `public record ContractParam(string Name, string? Type, TextPosition Position);`
- Update `StatementAst.Label` to `public record Label(string Name, ImmutableArray<ContractParam> Params, TextPosition Position)`.
- Update `ParseLabel` in `Parser.cs`:
    - After parsing name, loop while `Peek()` is `Star`.
    - Consume `Star`, `Identifier` (Name).
    - If `Match(Colon)`, consume `Identifier` (Type).
    - Add to params list.

### Step 2: Update Compiled Model

**Objective:** Store contracts in `CompiledStory`.

**Files:**
- `src/Zoh.Runtime/Execution/CompiledStory.cs`

**Changes:**
- Add `public ImmutableDictionary<string, ImmutableArray<ContractParam>> Contracts { get; }` property.
- Update constructor to accept this dictionary.
- Update `Parser.ParseStory` to populate this dictionary from `StatementAst.Label` nodes.

### Step 3: Implement Validation Logic

**Objective:** Enforce contracts at runtime.

**Files:**
- `src/Zoh.Runtime/Execution/Context.cs` (or Helper)
- `src/Zoh.Runtime/Verbs/Flow/JumpDriver.cs`
- `src/Zoh.Runtime/Verbs/Flow/CallDriver.cs` (and Fork)

**Changes:**
- Extension method `ValidateContract(this Context ctx, string checkpoint, CompiledStory story)`:
    - Look up contract in `story.Contracts`.
    - Iterate params.
    - Check `ctx.Variables.Get(name)`.
    - If `Nothing`, valid? Spec says "must not be nothing". Raise `checkpoint_violation`.
    - If `Type` is present, check type compatibility.
        - Need simple type mapping/checking logic (e.g. `val is ZohInteger`).
        - Raise `checkpoint_violation` on mismatch.

- Update `JumpDriver`, `CallDriver`, `ForkDriver`:
    - Before updating IP or creating new context, call `ValidateContract`.
    - For `Jump`, validate against *current* context state (transferred vars are already in store? No, `jump` can explicitly transfer story vars. Validating *after* jump resolution makes sense, but `jump` syntax is `/jump "story", "label", *vars`. Logic: `jump` driver copies vars if needed, THEN we validate).
    - Actually, `jump` simply moves IP. Variables are already in the store. So validation happens *before* moving IP? Or *after*?
    - Spec says: "referenced variables must not be resolved to nothing when the context is about to execute across or jump/fork/call to the checkpoint."
    - So correct place is: Before jumping (in current context) OR immediately after landing (in target context).
    - Since `jump` to another story drops story variables, the validation MUST happen *after* the jump logic prepares the target state but *before* execution continues.
    - For `Jump` within story: Context vars Unchanged. Just check existence.
    - For `Jump` to other story: Spec says "In order to transfer story-scoped variables, use the var parameters".
    - So the `JumpDriver` handles the transfer. Then we must validate that the *resulting* state satisfies the checkpoint.

### Step 4: Update Execution Loop

**Objective:** Enforce contracts when execution falls through to a checkpoint.

**Files:**
- `src/Zoh.Runtime/Execution/ZohRuntime.cs`

**Changes:**
- In `Run` method, inside the loop:
    - When `stmt` is `StatementAst.Label`:
        - Resolve label Name.
        - Call `ctx.ValidateContract(label.Name, ctx.CurrentStory)`.
        - If validation fails, `ctx.SetState(Terminated)` and break.

---

## Verification Plan

### Automated Checks
- [ ] Create `CheckpointContractTests.cs`.
    - Test parsing of `@chk *a *b:int`.
    - Test `Jump` to `@chk *a:int` with valid/invalid data.
    - Test `Call` and `Fork`.

### Manual Verification
- [ ] Run existing tests to ensure no regression.

---

## Rollback Plan
- Revert git changes.

## Notes
- Parsing `*var` after `@label` relies on `*` starting the param. This is unambiguous vs other statements (verbs start with `/`, labels with `@`).
