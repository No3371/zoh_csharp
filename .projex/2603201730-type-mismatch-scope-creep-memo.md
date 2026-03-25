# Memo: `type_mismatch` diagnostic code used outside `/set`

> **Date:** 2026-03-20
> **Author:** agent
> **Source Type:** Issue
> **Origin:** Conversation — user noted `type_mismatch` was originally introduced for `/set`

---

## Source

User stated that `type_mismatch` was created specifically for `/set` type validation. It now appears in `CollectionHelpers.cs` (line 79) for collection indexing operations as well.

---

## Context

During investigation of `invalid_arg` diagnostic codes, we found `type_mismatch` used in `csharp/src/Zoh.Runtime/Helpers/CollectionHelpers.cs:79` for a caught `InvalidCastException` during collection index assignment. This is outside its original `/set` scope.

Whether `type_mismatch` should remain scoped to `/set` or is acceptable as a general "wrong type" code affects which replacement code to use for the `invalid_arg` cleanup (see related plan).

---

## Related Projex

- 2603201730-diagnostic-code-invalid-arg-cleanup-plan.md (depends on this decision)
