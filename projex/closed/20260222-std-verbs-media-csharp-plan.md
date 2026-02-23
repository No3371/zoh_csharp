# Standard Verbs (Media) Plan

> **Status:** Complete
> **Completed:** 2026-02-23
> **Walkthrough:** [20260222-std-verbs-media-csharp-walkthrough.md](20260222-std-verbs-media-csharp-walkthrough.md)
> **Created:** 2026-02-22
> **Author:** Antigravity
> **Source:** Phase 4.5 of 20260207-csharp-runtime-nav.md
> **Related Projex:** [Navigation Roadmap](20260207-csharp-runtime-nav.md), [Presentation Plan](closed/20260222-std-verbs-presentation-csharp-plan.md)

---

## Summary

Implement the Phase 4.5 Standard Media Verbs (`/show`, `/hide`, `/play`, `/playOne`, `/stop`, `/pause`, `/resume`, `/setVolume`) using the independent Per-Driver model established in Phase 4.4. Each driver exposes a driver-specific handler interface for platform integration. All media verbs are **fire-and-forget** — they do not block execution or yield continuations.

**Scope:** `c#/src/Zoh.Runtime` (drivers, handler interfaces, validators, registration)
**Estimated Changes:** ~27 new files (8 drivers, 8 handler interfaces, 7 validators, registration additions), ~2 modified files

---

## Objective

### Problem / Gap / Need

ZOH stories need to direct visual and audio media (show images, play sounds, control playback). The runtime currently has no media verb drivers. The `spec/std_verbs.md` and `impl/10_std_verbs.md` define eight media verbs that must be implemented as decoupled drivers delegating to host-provided handler interfaces, following the pattern established by the Presentation verbs in Phase 4.4.

### Success Criteria

- [ ] `ShowDriver` resolves resource and all positioning/fade/opacity attributes, calls `IShowHandler`, returns the media id
- [ ] `HideDriver` resolves id and fade/easing attributes, calls `IHideHandler`
- [ ] `PlayDriver` resolves resource and volume/loops/easing attributes, calls `IPlayHandler`, returns the playback id
- [ ] `PlayOneDriver` resolves resource and volume/loops attributes, calls `IPlayOneHandler`, returns nothing
- [ ] `StopDriver` resolves optional id and fade/easing attributes, calls `IStopHandler` (stop-all when no id)
- [ ] `PauseDriver` resolves id, calls `IPauseHandler`
- [ ] `ResumeDriver` resolves id, calls `IResumeHandler`
- [ ] `SetVolumeDriver` resolves id + volume and fade/easing attributes, calls `ISetVolumeHandler`
- [ ] All drivers registered in `VerbRegistry.RegisterCoreVerbs()` with null handlers
- [ ] Validators registered in `HandlerRegistry.RegisterCoreHandlers()` for verbs with required params
- [ ] All drivers have comprehensive unit tests using mock handlers
- [ ] `dotnet build` and `dotnet test` pass

### Out of Scope

- Focus/Unfocus verbs (not in Phase 4.5 nav roadmap scope)
- Actual media playback implementations (host responsibility)
- Changes to `VerbContinuation` (media verbs are non-blocking)

---

## Context

### Current State

Phase 4.4 established the Per-Driver pattern in `Verbs/Standard/Presentation/`:
- Each driver accepts an optional `I*Handler` via constructor
- Constructs a request record, calls the handler, and returns
- Presentation drivers yield `HostContinuation` for user input; media drivers return `VerbResult.Ok()` immediately
- Helper methods (`ResolveAttributeToString`, `GetAttribute`, `HasAttribute`, `GetNamedParam`) are duplicated per-driver

Existing patterns (from `ConverseDriver.cs`):
- Context cast: `var ctx = context as Context;`
- Attribute resolution: `ResolveAttributeToString(call, "Style", ctx) ?? "dialog"`
- No handler: `_handler?.OnConverse(ctx, request);` → null-safe dispatch
- Return: `VerbResult.Ok()` or `VerbResult.Ok(new ZohStr(id))`

### Key Files

| File | Purpose | Changes Needed |
|------|---------|----------------|
| `Verbs/Standard/Media/*Driver.cs` | 8 new drivers | Create |
| `Verbs/Standard/Media/I*Handler.cs` | 8 handler interfaces + request records | Create |
| `Validation/Standard/Media/*Validator.cs` | 7 validators (Stop needs none) | Create |
| `Verbs/VerbRegistry.cs` | Core verb registration | Add 8 media registrations |
| `Execution/HandlerRegistry.cs` | Core handler registration | Add 7 validator registrations |

### Dependencies

- **Requires:** Phase 4.4 (Presentation) complete ✓
- **Blocks:** Phase 5 Integration Testing

### Constraints

- Spec (`spec/std_verbs.md`) is authoritative over `impl/10_std_verbs.md` per project rules
- Attribute matching is case-insensitive (confirmed in existing drivers)

### Spec vs Impl Discrepancies

| Topic | Spec | Impl | Decision |
|-------|------|------|----------|
| Show position attrs | `PosX`, `PosY`, `PosZ` | `x`, `y`, `z` | Use spec names; case-insensitive matching gives natural compat |
| Show size attrs | `RW`, `RH`, `Width`, `Height` | `rw`, `rh`, `w`, `h` | Use spec names |
| PlayOne return | "The id of the playback" | `return ok()` | Return nothing — fire-and-forget semantics override |
| Stop id param | Required in spec text but optional in examples | Optional in impl | Optional — `/stop;` stops all per both spec examples and impl |

---

## Implementation

### Overview

1. Create handler interfaces and request records (8 files)
2. Implement drivers (8 files)
3. Create validators (7 files)
4. Register drivers and validators (2 modified files)
5. Write unit tests (8 test files)

---

### Step 1: Show Handler & Driver

**Files (NEW):**
- `c#/src/Zoh.Runtime/Verbs/Standard/Media/IShowHandler.cs`
- `c#/src/Zoh.Runtime/Verbs/Standard/Media/ShowDriver.cs`

**Request record:**
```csharp
public record ShowRequest(
    string Resource, string Id,
    double? RelativeWidth, double? RelativeHeight,
    double? Width, double? Height,
    string Anchor, double PosX, double PosY, double PosZ,
    double FadeDuration, double Opacity, string Easing);
```

**Driver logic:** Resolve resource from first unnamed param. Read all attributes with defaults from spec. Call `_handler?.OnShow(ctx, request)`. Return `VerbResult.Ok(new ZohStr(id))`.

**Helper methods:** `ResolveAttributeToString`, `ResolveAttributeToDouble`, `GetAttribute` — same pattern as `ConverseDriver`.

---

### Step 2: Hide Handler & Driver

**Files (NEW):**
- `c#/src/Zoh.Runtime/Verbs/Standard/Media/IHideHandler.cs`
- `c#/src/Zoh.Runtime/Verbs/Standard/Media/HideDriver.cs`

**Request record:** `HideRequest(string Id, double FadeDuration, string Easing)`

**Driver logic:** Resolve id from first unnamed param. Read `Fade`/`Easing` attributes. Return `VerbResult.Ok()`.

---

### Step 3: Play Handler & Driver

**Files (NEW):**
- `c#/src/Zoh.Runtime/Verbs/Standard/Media/IPlayHandler.cs`
- `c#/src/Zoh.Runtime/Verbs/Standard/Media/PlayDriver.cs`

**Request record:** `PlayRequest(string Resource, string Id, double Volume, int Loops, string Easing)`

**Driver logic:** Resolve resource. Read `Id`/`Volume`/`Loops`/`Easing` attributes. Return `VerbResult.Ok(new ZohStr(id))`.

---

### Step 4: PlayOne Handler & Driver

**Files (NEW):**
- `c#/src/Zoh.Runtime/Verbs/Standard/Media/IPlayOneHandler.cs`
- `c#/src/Zoh.Runtime/Verbs/Standard/Media/PlayOneDriver.cs`

**Request record:** `PlayOneRequest(string Resource, double Volume, int Loops)`

**Driver logic:** Resolve resource. Read `Volume`/`Loops` attributes. Return `VerbResult.Ok()` (fire-and-forget, no id).

---

### Step 5: Stop Handler & Driver

**Files (NEW):**
- `c#/src/Zoh.Runtime/Verbs/Standard/Media/IStopHandler.cs`
- `c#/src/Zoh.Runtime/Verbs/Standard/Media/StopDriver.cs`

**Request record:** `StopRequest(string? Id, double FadeDuration, string Easing)` — nullable `Id` for stop-all.

**Driver logic:** Read `Fade`/`Easing` attributes. If unnamed params present, resolve id; otherwise `null`. Return `VerbResult.Ok()`.

---

### Step 6: Pause & Resume Handlers & Drivers

**Files (NEW):**
- `c#/src/Zoh.Runtime/Verbs/Standard/Media/IPauseHandler.cs`
- `c#/src/Zoh.Runtime/Verbs/Standard/Media/PauseDriver.cs`
- `c#/src/Zoh.Runtime/Verbs/Standard/Media/IResumeHandler.cs`
- `c#/src/Zoh.Runtime/Verbs/Standard/Media/ResumeDriver.cs`

**Request records:** `PauseRequest(string Id)`, `ResumeRequest(string Id)`

**Driver logic:** Minimal — resolve id, call handler, return `VerbResult.Ok()`.

> [!NOTE]
> The verb name `resume` does not conflict with `Context.Resume()`. The verb is `std.resume` (driver), the context method is for host resume logic. Different namespaces, no collision.

---

### Step 7: SetVolume Handler & Driver

**Files (NEW):**
- `c#/src/Zoh.Runtime/Verbs/Standard/Media/ISetVolumeHandler.cs`
- `c#/src/Zoh.Runtime/Verbs/Standard/Media/SetVolumeDriver.cs`

**Request record:** `SetVolumeRequest(string Id, double Volume, double FadeDuration, string Easing)`

**Driver logic:** Resolve id (param 0), volume (param 1), read `Fade`/`Easing` attributes. Return `VerbResult.Ok()`.

---

### Step 8: Validators

**Files (NEW):** All in `c#/src/Zoh.Runtime/Validation/Standard/Media/`
- `ShowValidator.cs` — `/show` requires ≥1 unnamed param
- `HideValidator.cs` — `/hide` requires ≥1 unnamed param
- `PlayValidator.cs` — `/play` requires ≥1 unnamed param
- `PlayOneValidator.cs` — `/playOne` requires ≥1 unnamed param
- `PauseValidator.cs` — `/pause` requires ≥1 unnamed param
- `ResumeValidator.cs` — `/resume` requires ≥1 unnamed param
- `SetVolumeValidator.cs` — `/setVolume` requires ≥2 unnamed params

**No validator for `/stop`** — 0 params is valid (stop-all).

All follow the same minimal pattern as `ConverseValidator`:
```csharp
public class ShowValidator : IVerbValidator
{
    public string VerbName => "show";
    public int Priority => 100;

    public IReadOnlyList<Diagnostic> Validate(VerbCallAst call, CompiledStory story)
    {
        var diagnostics = new List<Diagnostic>();
        if (call.UnnamedParams.Length < 1)
            diagnostics.Add(new Diagnostic(DiagnosticSeverity.Fatal, "missing_parameter",
                "/show requires at least 1 parameter (the resource).", call.Start, story.Name));
        return diagnostics;
    }
}
```

---

### Step 9: Registration

**Files (MODIFY):**
- `c#/src/Zoh.Runtime/Verbs/VerbRegistry.cs` — in `RegisterCoreVerbs()`
- `c#/src/Zoh.Runtime/Execution/HandlerRegistry.cs` — in `RegisterCoreHandlers()`

**VerbRegistry additions:**
```csharp
// Media Verbs
Register(new Standard.Media.ShowDriver());
Register(new Standard.Media.HideDriver());
Register(new Standard.Media.PlayDriver());
Register(new Standard.Media.PlayOneDriver());
Register(new Standard.Media.StopDriver());
Register(new Standard.Media.PauseDriver());
Register(new Standard.Media.ResumeDriver());
Register(new Standard.Media.SetVolumeDriver());
```

**HandlerRegistry additions:**
```csharp
// Standard Media Validators
RegisterVerbValidator(new Validation.Standard.Media.ShowValidator());
RegisterVerbValidator(new Validation.Standard.Media.HideValidator());
RegisterVerbValidator(new Validation.Standard.Media.PlayValidator());
RegisterVerbValidator(new Validation.Standard.Media.PlayOneValidator());
RegisterVerbValidator(new Validation.Standard.Media.PauseValidator());
RegisterVerbValidator(new Validation.Standard.Media.ResumeValidator());
RegisterVerbValidator(new Validation.Standard.Media.SetVolumeValidator());
```

---

### Step 10: Unit Tests

**Files (NEW):** All in `c#/tests/Zoh.Tests/Verbs/Standard/Media/`

**Test pattern** (following `ConverseDriverTests.cs`):
1. Create `Mock*Handler` implementing `I*Handler` that records requests in a `List<*Request>`
2. Create `ZohRuntime`, call `Handlers.RegisterCoreHandlers()`, override driver with mock
3. Load ZOH story, run context
4. Assert handler called with correct parameters
5. Assert `ContextState.Terminated` (fire-and-forget, no blocking)

**Test cases:**

| File | Test Cases |
|------|------------|
| `ShowDriverTests.cs` | Basic show; all attributes parsed; returns id; default id = resource; no handler = no crash |
| `HideDriverTests.cs` | Basic hide; fade/easing attributes |
| `PlayDriverTests.cs` | Basic play; volume/loops/easing; returns id |
| `PlayOneDriverTests.cs` | Basic playOne; returns nothing |
| `StopDriverTests.cs` | Stop with id; stop all (no id); fade attributes |
| `PauseDriverTests.cs` | Basic pause |
| `ResumeDriverTests.cs` | Basic resume |
| `SetVolumeDriverTests.cs` | Basic setVolume; fade attributes |

---

## Verification Plan

### Automated Checks
- [ ] `cd s:\repos\zoh\c#; dotnet build` — compiles without errors
- [ ] `cd s:\repos\zoh\c#; dotnet test` — all existing tests still pass
- [ ] `cd s:\repos\zoh\c#; dotnet test --filter "FullyQualifiedName~Zoh.Tests.Verbs.Standard.Media"` — all new media tests pass

### Acceptance Criteria Validation

| Criterion | How to Verify | Expected Result |
|-----------|---------------|-----------------|
| All 8 drivers registered | Check `VerbRegistry.RegisterCoreVerbs()` | 8 media verb registrations present |
| Fire-and-forget | Tests assert `ContextState.Terminated` | No test shows `WaitingHost` |
| Per-driver handlers | Inspect constructors | Each driver takes its own `I*Handler?` |
| Returns correct values | Tests assert return values | Show/Play return id string, others nothing |
| Validators catch errors | Build test script `/show;` with no params | Produces fatal diagnostic |

---

## Rollback Plan

1. Delete `Verbs/Standard/Media/` directory
2. Delete `Validation/Standard/Media/` directory
3. Revert registration lines in `VerbRegistry.cs` and `HandlerRegistry.cs`
4. Delete test files in `Zoh.Tests/Verbs/Standard/Media/`

---

## Notes

### Assumptions
- Attribute matching is case-insensitive (verified in existing drivers via `StringComparison.OrdinalIgnoreCase`)
- `ValueResolver.Resolve` handles literals and variable references uniformly
- Media verbs do not need `VerbContinuation`; they are purely imperative commands
- `VerbResult.Ok(new ZohStr(id))` is the pattern for returning string values

### Risks
- **Attribute name divergence:** Spec uses `PosX`/`PosY`/`PosZ` but ZOH script authors might prefer shorter forms. Since attribute lookup is case-insensitive, the spec forms work naturally and authors can use `posx` etc. freely.
- **Volume clamping:** Spec doesn't define whether volume should be clamped to 0–1. Decision: leave clamping to host handlers; drivers pass raw values.

### Open Questions
- None.
