# Memo: Presentation Driver Timeout Continuations Missing INFO Diagnostic

> **Date:** 2026-03-05
> **Author:** agent
> **Source Type:** Issue
> **Origin:** Post-execution analysis during std-verbs-driver-alignment followup (`20260304-std-verbs-driver-alignment-plan-review.md`)

---

## Source

Noticed during review of whether a C# followup was needed for `20260304-std-verbs-driver-alignment-plan.md`: all four presentation drivers (`ConverseDriver`, `ChooseDriver`, `ChooseFromDriver`, `PromptDriver`) return `DriverResult.Complete.Ok()` on the `WaitTimedOut` path, but `impl/10_std_verbs.md` specifies that timeout continuations should produce a `Diagnostic(INFO, "timeout", "...")` entry on the context.

---

## Context

The `WaitTimedOut` branch in each driver looks like:

```csharp
if (waitResult is WaitTimedOut)
    return DriverResult.Complete.Ok();
```

The spec (`impl/10_std_verbs.md`) states that when a presentation verb's wait times out, execution continues normally **and** an INFO-level diagnostic is appended: `Diagnostic(INFO, "timeout", "<verb> timed out")`. The diagnostic is how the story author can detect that a timeout occurred (e.g., read via `/diagnose`).

The gap: the diagnostic is silently dropped, so authors have no way to distinguish "user dismissed the dialogue" from "dialogue timed out."

This was deferred rather than fixed during execution of `20260305-runtime-api-surface-alignment-plan.md` because it was out of scope for that plan.

---

## Related Projex

- `20260304-std-verbs-driver-alignment-plan-review.md`
- `20260305-runtime-api-surface-alignment-plan.md`
