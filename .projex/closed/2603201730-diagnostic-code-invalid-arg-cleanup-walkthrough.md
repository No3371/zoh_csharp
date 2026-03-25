# Walkthrough - Diagnostic Code Cleanup (invalid_arg)

Execution of `2603201730-diagnostic-code-invalid-arg-cleanup-plan.md`.

## Summary

Replaced all occurrences of `invalid_arg` and `invalid_args` with `invalid_type` and `invalid_params` respectively across 7 driver files. Corrected severity in `RollDriver`.

## Changes

### C# Runtime

#### [src/Zoh.Runtime/Verbs/Flow/JumpDriver.cs](csharp/src/Zoh.Runtime/Verbs/Flow/JumpDriver.cs)
- Replaced `invalid_arg` with `invalid_type` (3 sites)

#### [src/Zoh.Runtime/Verbs/Flow/ForkDriver.cs](csharp/src/Zoh.Runtime/Verbs/Flow/ForkDriver.cs)
- Replaced `invalid_arg` with `invalid_type` (2 sites)

#### [src/Zoh.Runtime/Verbs/Flow/CallDriver.cs](csharp/src/Zoh.Runtime/Verbs/Flow/CallDriver.cs)
- Replaced `invalid_arg` with `invalid_type` (3 sites)

#### [src/Zoh.Runtime/Verbs/Flow/SleepDriver.cs](csharp/src/Zoh.Runtime/Verbs/Flow/SleepDriver.cs)
- Replaced `invalid_arg` with `invalid_type` (1 site)

#### [src/Zoh.Runtime/Verbs/Signals/WaitDriver.cs](csharp/src/Zoh.Runtime/Verbs/Signals/WaitDriver.cs)
- Replaced `invalid_arg` with `invalid_type` (1 site)

#### [src/Zoh.Runtime/Verbs/Signals/SignalDriver.cs](csharp/src/Zoh.Runtime/Verbs/Signals/SignalDriver.cs)
- Replaced `invalid_arg` with `invalid_type` (1 site)

#### [src/Zoh.Runtime/Verbs/Core/RollDriver.cs](csharp/src/Zoh.Runtime/Verbs/Core/RollDriver.cs)
- Replaced `invalid_args` with `invalid_params` (1 site)
- Changed severity from `Error` to `Fatal` for consistency.

## Verification Results

### Automated Tests
- `dotnet build`: Success
- `dotnet test`: 704 passed, 0 failed.

### Manual Verification
- `rg "invalid_arg" src/`: 0 matches
- `rg "invalid_args" src/`: 0 matches
- Code review confirmed `invalid_type` used for type checks and `invalid_params` for structural checks.
