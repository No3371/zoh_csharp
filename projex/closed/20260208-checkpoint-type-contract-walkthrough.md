# Walkthrough: Checkpoint Type Contract Implementation

**Status:** Complete
**Branch:** `projex/20260208-checkpoint-type-contract-impl`

## Summary
Implemented optional type contracts for checkpoints in the C# runtime. This allows checkpoints to specify the expected types of incoming variables, enforcing type safety at story boundaries.

## Changes

### 1. AST and Parser
- Modified `StatementAst.Label` to include `ImmutableArray<ContractParam>`.
- Updated `Parser.ParseLabel` to parse `@checkpoint *var` and `@checkpoint *var:type` syntax.
    - Implemented lookahead logic to disambiguate between contract parameters and subsequent variable assignment statements (e.g., `@checkpoint *param` vs `*var <- value`).
- Supports all standard ZOH types: `string`, `integer`, `boolean`, `double`, `list`, `map`, `channel`, `verb`, `expression`.

### 2. Compiled Model
- Updated `CompiledStory.cs` to store a `Contracts` dictionary mapping checkpoint names to their parameter lists.

### 3. Runtime Validation
- Added `Context.ValidateContract(checkpointName)` method.
- Checks for:
    - **Existence**: Variable must not be `nothing`.
    - **Type**: Variable value must match the specified type (if any).
- Strict type name validation: Unknown type names in contracts cause validation failure (fail fast).

### 4. Navigation Enforcement
- Updated `JumpDriver`, `CallDriver`, and `ForkDriver` to call `ValidateContract` before transferring control or scheduling new contexts.
- Updated `ZohRuntime.Run` loop to validate contracts when execution falls through to a label.

## Verification

### Automated Tests
- Created `CheckpointContractTests.cs`.
- Verified:
    - Success when variables match requirements.
    - Failure when variables are `nothing`.
    - Failure when types mismatch.
    - Support for all proposed types.
    - Support for untyped wildcard parameters.

### Manual Verification
- Ran existing `NavigationTests` and `ConcurrencyTests` to ensure no regressions. All passed.

## Next Steps
- Merge the ephemeral branch `projex/20260208-checkpoint-type-contract-impl` to `main`.
