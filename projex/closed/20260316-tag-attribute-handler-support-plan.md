# [tag] Attribute Handler Support — C# Implementation

> **Status:** Complete (2026-03-16)
> **Patch:** 20260316-tag-attribute-handler-support-patch.md
> **Created:** 2026-03-16
> **Author:** Claude
> **Source:** Spec commit `430d3cb`; spec `std_attributes.md`
> **Related Projex:** `20260316-spec-catchup-followup.md`
> **Worktree:** Yes

---

## Summary

Surface the `[tag]` standard attribute in presentation verb handler request records so hosts can use tags for logging, analytics, and element tracking. The attribute already parses and stores correctly — this plan adds driver-to-handler passthrough.

**Scope:** Presentation verb request records and driver tag extraction
**Estimated Changes:** 17 files modified (5 request records, 6 drivers, 6 test files)

---

## Objective

### Problem / Gap / Need

The spec (`std_attributes.md`) defines `[tag]` as a standard attribute for tagging verb calls with arbitrary strings. The parser already handles `[tag: "value"]` — attributes are stored in `VerbCallAst.Attributes` without validation. However, no verb driver extracts or passes the tag to its handler, so hosts can't use it.

### Success Criteria

- [x] `ShowRequest`, `ConverseRequest`, `ChooseRequest`, `PromptRequest`, `PlayRequest` include `string? Tag` field
- [x] Corresponding drivers extract `[tag]` attribute and pass to handler
- [x] Hosts receive tag in request records
- [ ] `dotnet test` passes (not runnable in this environment)
- [x] Existing handler implementations compile without changes (Tag is nullable with default)

### Out of Scope

- Tag validation (non-empty, format constraints)
- Non-presentation verbs (core verbs don't need tags — they're internal)
- Tag-based filtering or querying

---

## Context

### Current State

Attribute parsing is generic — `[tag: "value"]` already works syntactically. Drivers extract attributes using a `GetAttribute`/`ResolveAttributeToString` helper pattern (e.g., ShowDriver extracts `fade`, `easing`, etc.). Request records are plain C# records passed to `IShowHandler.OnShow`, `IChooseHandler.OnChoose`, etc.

Previously, no driver read or surfaced `[tag]`.

**Patched:** Drivers now extract and pass `[tag]` to handlers (see Patch link in header).

### Key Files

| File | Role | Change Summary |
|------|------|----------------|
| `src/Zoh.Runtime/Verbs/Standard/Media/ShowDriver.cs` | `/show` driver | Extract tag, pass to ShowRequest |
| `src/Zoh.Runtime/Verbs/Standard/Presentation/ConverseDriver.cs` | `/converse` driver | Extract tag, pass to ConverseRequest |
| `src/Zoh.Runtime/Verbs/Standard/Presentation/ChooseDriver.cs` | `/choose` driver | Extract tag, pass to ChooseRequest |
| `src/Zoh.Runtime/Verbs/Standard/Presentation/ChooseFromDriver.cs` | `/chooseFrom` driver | Extract tag, pass to ChooseRequest |
| `src/Zoh.Runtime/Verbs/Standard/Presentation/PromptDriver.cs` | `/prompt` driver | Extract tag, pass to PromptRequest |
| `src/Zoh.Runtime/Verbs/Standard/Media/PlayDriver.cs` | `/play` driver | Extract tag, pass to PlayRequest |
| Handler interface files (requests) | Request records | Add `string? Tag` field |

### Dependencies

- **Requires:** None
- **Blocks:** Nothing

### Constraints

- `Tag` must be nullable with default null — existing handler implementations must compile without changes
- Use existing `ResolveAttributeToString` pattern for extraction

### Assumptions

- Request records use C# `record` types and can be extended with optional trailing parameters
- All presentation drivers already have a `GetAttribute` or equivalent helper

### Impact Analysis

- **Direct:** Presentation driver files and their request records
- **Adjacent:** Host handler implementations — they'll see a new field but don't need to use it
- **Downstream:** Hosts that want tag-based logging/tracking can now access it

---

## Implementation

### Overview

For each presentation driver: (1) add `string? Tag` to the request record, (2) extract `[tag]` in the driver, (3) pass to request constructor. Pattern is identical across all drivers.

### Step 1: Add Tag to request records

**Objective:** Extend each request record with nullable Tag field.
**Confidence:** High
**Depends on:** None

**Files:**
- Handler interface files containing `ShowRequest`, `ConverseLine`/`ConverseRequest`, `ChooseRequest`, `PromptRequest`, `PlayRequest`

**Changes:**

For each request record, add `string? Tag = null` as the last parameter:

```csharp
// Example for ShowRequest:
// Before:
public record ShowRequest(string Resource, string Id, /* ... */, string Easing);

// After:
public record ShowRequest(string Resource, string Id, /* ... */, string Easing, string? Tag = null);
```

Default `null` ensures existing constructor calls compile without changes.

**Verification:** `dotnet build` succeeds. Existing handler implementations unaffected.

### Step 2: Extract and pass tag in each driver

**Objective:** Each presentation driver reads `[tag]` and passes it to the request.
**Confidence:** High
**Depends on:** Step 1

**Files:**
- `ShowDriver.cs`, `ConverseDriver.cs`, `ChooseDriver.cs`, `PromptDriver.cs`, `PlayDriver.cs`

**Changes:**

In each driver's `Execute` method, after existing attribute extraction:

```csharp
var tag = ResolveAttributeToString(call, "tag", ctx);
```

Then pass `Tag: tag` to the request constructor.

If a driver lacks `ResolveAttributeToString`, add it (same helper pattern used in ShowDriver):

```csharp
private string? ResolveAttributeToString(VerbCallAst call, string name, IExecutionContext ctx)
{
    var attr = call.Attributes
        .FirstOrDefault(a => a.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    if (attr?.Value == null) return null;
    var val = ValueResolver.Resolve(attr.Value, ctx);
    return val is ZohStr s ? s.Value : val?.ToString();
}
```

**Verification:** `/show [tag: "intro"] "bg.jpg";` → handler receives `ShowRequest` with `Tag = "intro"`.

---

## Verification Plan

### Automated Checks

- [ ] `dotnet build` succeeds
- [ ] `dotnet test` — all existing tests pass
- [ ] New test: tag attribute extracted and passed to request
- [ ] New test: missing tag → Tag is null

### Acceptance Criteria Validation

| Criterion | How to Verify | Expected Result |
|-----------|---------------|-----------------|
| Tag in request | `/show [tag: "x"] "r";` | `ShowRequest.Tag == "x"` |
| Tag absent | `/show "r";` | `ShowRequest.Tag == null` |
| Variable tag | `/show [tag: *t] "r";` | `ShowRequest.Tag` == resolved value |
| Compile compat | Build without handler changes | No errors |

---

## Rollback Plan

1. Remove `Tag` parameter from request records
2. Remove tag extraction lines from drivers
3. All changes are additive — no behavioral modification

---

## Notes

### Risks

- **Record parameter ordering:** Adding a trailing optional parameter to C# records is safe for positional construction but may affect pattern matching if anyone deconstructs by position. Mitigation: unlikely — request records are consumed by property access.

### Open Questions

None.
