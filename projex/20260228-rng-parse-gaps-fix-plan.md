# Fix Audit Gaps 3.4 — WRoll Negative-Weight Fatal & Parse List/Map

> **Status:** Ready
> **Created:** 2026-02-28
> **Author:** Antigravity
> **Source:** Direct request — gaps discovered in [20260223-csharp-spec-audit-nav.md](20260223-csharp-spec-audit-nav.md) milestone 3.4
> **Related Projex:** [20260223-csharp-spec-audit-nav.md](20260223-csharp-spec-audit-nav.md)

---

## Summary

Two compliance gaps remain open from the audit of Core RNG & Parsing verbs:

1. **GAP 1 — `/wroll` negative weight**: The driver silently clamps negative weights to `0` instead of raising a fatal `invalid_value` diagnostic per the spec (`Core.WRoll` → Diagnostics).
2. **GAP 2 — `/parse` list/map**: The driver returns a stub `not_implemented` fatal for `list` and `map` targets; the spec requires full JSON-like collection parsing.

**Scope:** `c#/src/Zoh.Runtime/Verbs/Core/RollDriver.cs`, `c#/src/Zoh.Runtime/Verbs/Core/ParseDriver.cs`, and their test companions.
**Estimated Changes:** 2 source files, 1 existing test file updated, 1 new test file added.

---

## Objective

### Problem / Gap / Need

**GAP 1 (`RollDriver.cs`, `ExecuteWRoll`):**
```csharp
// Line 61 — current behaviour (WRONG):
if (w < 0) w = 0;
```
The spec (`Core.WRoll → Diagnostics`) states:
- Fatal `invalid_type`: A weight is not a number.
- Fatal `invalid_value`: A weight is **negative**.

Silently clamping violates both the letter and spirit of the spec.

**GAP 2 (`ParseDriver.cs`):**
```csharp
// Lines 42–43 — current behaviour (WRONG):
"list" => VerbResult.Fatal(..., "not_implemented", "List parsing not yet supported", ...),
"map"  => VerbResult.Fatal(..., "not_implemented", "Map parsing not yet supported", ...),
```
Spec (`Core.Parse`) requires `list` and `map` to be valid targets.  The expected format derived from the ZOH spec and existing `ZohValue` types is standard JSON:
- `list` → JSON array, e.g. `[1, "a", true]`
- `map`  → JSON object with string keys, e.g. `{"key": 123}`

`System.Text.Json` is already available in the runtime project (used by persistence layer).  The deserialization needs to recursively convert `JsonElement` → `ZohValue`.

### Success Criteria
- [ ] `/wroll` with a negative weight raises a fatal `invalid_value` diagnostic (not `not_implemented`).
- [ ] `/parse "[1, \"a\", true]", "list"` returns a `ZohList` with the correct three elements.
- [ ] `/parse "{\"x\": 1}", "map"` returns a `ZohMap` with the correct entry.
- [ ] `/parse "[1,2]"` (inferred) returns a `ZohList` (inference heuristic already identifies `[` → `list`).
- [ ] `/parse "{\"k\":1}"` (inferred) returns a `ZohMap`.
- [ ] Malformed JSON (e.g. `"[1,2"`) produces a fatal `invalid_format` diagnostic.
- [ ] All pre-existing `ParseTests` and `CoreVerbTests` pass unchanged.
- [ ] `dotnet build` and `dotnet test` pass with no warnings introduced.

### Out of Scope
- Any other audit gaps (3.1, 3.2, 3.3, 4.x, 5.x, …).
- Supporting non-JSON list/map syntax (ZOH literal syntax inside `/parse`).

---

## Context

### Current State

**`RollDriver.cs` — `ExecuteWRoll`** (lines 56–65):
```csharp
for (int i = 0; i < args.Length; i += 2)
{
    var val  = ValueResolver.Resolve(args[i],     context);
    var wVal = ValueResolver.Resolve(args[i + 1], context);
    int w = (int)wVal.AsInt().Value;
    if (w < 0) w = 0;          // ← BUG: silent clamp
    pairs.Add((val, w));
    totalWeight += w;
}
```
`wVal.AsInt()` throws/returns a default if the weight is not numeric — the spec also wants a fatal `invalid_type` in that path, but `AsInt()` on a non-numeric value currently returns a default `ZohInt(0)`, so it would be swallowed as well.  We must add explicit type checks before the cast.

**`ParseDriver.cs` — `Execute` switch** (lines 42–43):
```csharp
"list" => VerbResult.Fatal(..., "not_implemented", ...),
"map"  => VerbResult.Fatal(..., "not_implemented", ...),
```

**Existing test coverage:**
- `c#/tests/Zoh.Tests/Verbs/Core/ParseTests.cs` — covers integer/double/boolean parsing and type inference (currently asserts `not_implemented` for list/map inference, which will change).
- No dedicated roll/wroll test file; wroll behaviour is not tested at all.

### Key Files

| File | Purpose | Changes Needed |
|------|---------|----------------|
| `c#/src/Zoh.Runtime/Verbs/Core/RollDriver.cs` | Implements `/roll`, `/wroll`, `/rand` | Replace silent clamp with fatal diagnostic |
| `c#/src/Zoh.Runtime/Verbs/Core/ParseDriver.cs` | Implements `/parse` | Implement `list`/`map` branches via `System.Text.Json` |
| `c#/tests/Zoh.Tests/Verbs/Core/ParseTests.cs` | Unit tests for `/parse` | Update inference tests; add list/map success tests |
| `c#/tests/Zoh.Tests/Verbs/Core/RollTests.cs` | Unit tests for `/roll`, `/wroll`, `/rand` | **[NEW]** Add wroll negative-weight fatal test |

### Dependencies
- **Requires:** Nothing — both gaps are self-contained.
- **Blocks:** Nothing — no known projex waiting on these.

### Constraints
- Must not break existing passing tests.
- `System.Text.Json` is already a transitive dependency; no new NuGet package required.
- JSON parsing must support nested arrays and objects (recursive `ZohValue` conversion).

---

## Implementation

### Overview

Two independent fixes applied in parallel: a one-line guard in `RollDriver` and a JSON-based list/map implementation in `ParseDriver`, paired with test additions.

---

### Step 1: Fix `/wroll` Negative-Weight Handling (`RollDriver.cs`)

**Objective:** Replace the silent clamp with proper `invalid_type` (non-numeric weight) and `invalid_value` (negative weight) fatals.

**Files:**
- `c#/src/Zoh.Runtime/Verbs/Core/RollDriver.cs`

**Changes:**

```csharp
// Before (lines 56–65):
for (int i = 0; i < args.Length; i += 2)
{
    var val  = ValueResolver.Resolve(args[i],     context);
    var wVal = ValueResolver.Resolve(args[i + 1], context);
    int w = (int)wVal.AsInt().Value;
    if (w < 0) w = 0;
    pairs.Add((val, w));
    totalWeight += w;
}

// After:
for (int i = 0; i < args.Length; i += 2)
{
    var val  = ValueResolver.Resolve(args[i],     context);
    var wVal = ValueResolver.Resolve(args[i + 1], context);

    if (wVal is not ZohInt weightInt)
        return VerbResult.Fatal(new Diagnostics.Diagnostic(
            Diagnostics.DiagnosticSeverity.Error, "invalid_type",
            $"wroll weight at position {i + 1} must be an integer, got {wVal.Type}", verb.Start));

    long w = weightInt.Value;

    if (w < 0)
        return VerbResult.Fatal(new Diagnostics.Diagnostic(
            Diagnostics.DiagnosticSeverity.Error, "invalid_value",
            $"wroll weight at position {i + 1} must be non-negative, got {w}", verb.Start));

    pairs.Add((val, (int)w));
    totalWeight += (int)w;
}
```

**Rationale:** Spec explicitly lists both `invalid_type` (non-numeric weight) and `invalid_value` (negative weight) as fatal diagnostics. Explicit type check via pattern matching avoids relying on `AsInt()`'s default-fallback behaviour.

**Verification:** New unit test `WRoll_NegativeWeight_ReturnsFatal` (Step 3).

---

### Step 2: Implement `/parse` List and Map (`ParseDriver.cs`)

**Objective:** Replace the `not_implemented` stubs with real JSON-based deserialization.

**Files:**
- `c#/src/Zoh.Runtime/Verbs/Core/ParseDriver.cs`

**Changes — add `using` and helper method, replace stubs:**

```csharp
// Add at top of file:
using System.Text.Json;

// Replace switch arms (lines 42–43):
"list" => ParseList(str, verb),
"map"  => ParseMap(str, verb),

// Add private helper methods to ParseDriver class:

private VerbResult ParseList(string str, VerbCallAst verb)
{
    try
    {
        using var doc = JsonDocument.Parse(str);
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
            return VerbResult.Fatal(new Diagnostics.Diagnostic(
                Diagnostics.DiagnosticSeverity.Error, "invalid_format",
                $"Cannot parse '{str}' as list: not a JSON array", verb.Start));

        var items = doc.RootElement.EnumerateArray()
                       .Select(e => JsonElementToZohValue(e))
                       .ToImmutableArray();
        return VerbResult.Ok(new ZohList(items));
    }
    catch (JsonException)
    {
        return VerbResult.Fatal(new Diagnostics.Diagnostic(
            Diagnostics.DiagnosticSeverity.Error, "invalid_format",
            $"Cannot parse '{str}' as list: malformed JSON", verb.Start));
    }
}

private VerbResult ParseMap(string str, VerbCallAst verb)
{
    try
    {
        using var doc = JsonDocument.Parse(str);
        if (doc.RootElement.ValueKind != JsonValueKind.Object)
            return VerbResult.Fatal(new Diagnostics.Diagnostic(
                Diagnostics.DiagnosticSeverity.Error, "invalid_format",
                $"Cannot parse '{str}' as map: not a JSON object", verb.Start));

        var builder = ImmutableDictionary.CreateBuilder<string, ZohValue>();
        foreach (var prop in doc.RootElement.EnumerateObject())
            builder[prop.Name] = JsonElementToZohValue(prop.Value);
        return VerbResult.Ok(new ZohMap(builder.ToImmutable()));
    }
    catch (JsonException)
    {
        return VerbResult.Fatal(new Diagnostics.Diagnostic(
            Diagnostics.DiagnosticSeverity.Error, "invalid_format",
            $"Cannot parse '{str}' as map: malformed JSON", verb.Start));
    }
}

private static ZohValue JsonElementToZohValue(JsonElement el) => el.ValueKind switch
{
    JsonValueKind.Null      => ZohValue.Nothing,
    JsonValueKind.True      => new ZohBool(true),
    JsonValueKind.False     => new ZohBool(false),
    JsonValueKind.Number when el.TryGetInt64(out long l) => new ZohInt(l),
    JsonValueKind.Number    => new ZohFloat(el.GetDouble()),
    JsonValueKind.String    => new ZohStr(el.GetString()!),
    JsonValueKind.Array     => new ZohList(el.EnumerateArray()
                                             .Select(JsonElementToZohValue)
                                             .ToImmutableArray()),
    JsonValueKind.Object    =>
        new ZohMap(el.EnumerateObject()
                     .ToImmutableDictionary(p => p.Name,
                                            p => JsonElementToZohValue(p.Value))),
    _ => ZohValue.Nothing
};
```

Also add the missing `using System.Collections.Immutable;` if not already present (it is already in the file for `ImmutableArray`).

**Rationale:** `System.Text.Json` is already available; `JsonElement` conversion maps cleanly onto ZOH's type hierarchy. Numbers prefer integer when the value fits `Int64`; otherwise fall back to double — matching spec semantics.

**Verification:** New unit tests in `ParseTests.cs` (Step 3).

---

### Step 3: Add/Update Tests

**Objective:** Cover both fixes with passing tests; remove obsolete `not_implemented` assertions for list/map inference.

#### 3a. Update `ParseTests.cs`

**File:** `c#/tests/Zoh.Tests/Verbs/Core/ParseTests.cs`

The existing `Parse_Inference_WithWhitespace` theory includes two cases that currently assert `not_implemented`:
```csharp
[InlineData("  [1, 2]  ", "list")]
[InlineData("  {a:1}  ", "map")]
```
These assertions must be updated. However — the `{a:1}` case is **not valid JSON** (unquoted key). The `InferType` heuristic routes it to `map`, but `ParseMap` will reject it as malformed JSON and return `invalid_format`.  
**Resolution:** Change the inline data to use valid JSON: `{"a":1}`.

Also add explicit success tests:

```csharp
[Fact]
public void Parse_List_FromJson()
{
    var call = MakeParseCall("[1, \"hello\", true]", "list");
    var result = _driver.Execute(_context, call);
    Assert.True(result.IsSuccess, result.Diagnostics.FirstOrDefault()?.Message);
    var list = Assert.IsType<ZohList>(result.Value);
    Assert.Equal(3, list.Items.Length);
    Assert.Equal(new ZohInt(1),       list.Items[0]);
    Assert.Equal(new ZohStr("hello"), list.Items[1]);
    Assert.Equal(new ZohBool(true),   list.Items[2]);
}

[Fact]
public void Parse_Map_FromJson()
{
    var call = MakeParseCall("{\"score\": 42, \"name\": \"hero\"}", "map");
    var result = _driver.Execute(_context, call);
    Assert.True(result.IsSuccess, result.Diagnostics.FirstOrDefault()?.Message);
    var map = Assert.IsType<ZohMap>(result.Value);
    Assert.Equal(new ZohInt(42),       map.Items["score"]);
    Assert.Equal(new ZohStr("hero"),   map.Items["name"]);
}

[Fact]
public void Parse_List_Inferred()
{
    var call = MakeParseCall("[10, 20]");
    var result = _driver.Execute(_context, call);
    Assert.True(result.IsSuccess);
    Assert.IsType<ZohList>(result.Value);
}

[Fact]
public void Parse_Map_Inferred()
{
    var call = MakeParseCall("{\"k\": 1}");
    var result = _driver.Execute(_context, call);
    Assert.True(result.IsSuccess);
    Assert.IsType<ZohMap>(result.Value);
}

[Fact]
public void Parse_MalformedList_ReturnsFatal()
{
    var call = MakeParseCall("[1, 2", "list");
    var result = _driver.Execute(_context, call);
    Assert.False(result.IsSuccess);
    Assert.Equal("invalid_format", result.Diagnostics[0].Code);
}

[Fact]
public void Parse_NestedStructure()
{
    var call = MakeParseCall("{\"items\": [1, 2, 3]}", "map");
    var result = _driver.Execute(_context, call);
    Assert.True(result.IsSuccess);
    var map = Assert.IsType<ZohMap>(result.Value);
    var inner = Assert.IsType<ZohList>(map.Items["items"]);
    Assert.Equal(3, inner.Items.Length);
}
```

#### 3b. Add `RollTests.cs`

**File:** `c#/tests/Zoh.Tests/Verbs/Core/RollTests.cs` **[NEW]**

```csharp
using System.Collections.Immutable;
using Xunit;
using Zoh.Runtime.Execution;
using Zoh.Runtime.Parsing.Ast;
using Zoh.Runtime.Types;
using Zoh.Runtime.Verbs.Core;
using Zoh.Tests.Execution;

namespace Zoh.Tests.Verbs.Core;

public class RollTests
{
    private readonly TestExecutionContext _context = new();
    private readonly RollDriver _driver = new();

    private VerbCallAst MakeWRollCall(params object[] valWeightPairs)
    {
        var @params = new List<ValueAst>();
        for (int i = 0; i < valWeightPairs.Length; i++)
        {
            var raw = valWeightPairs[i];
            @params.Add(raw is int n
                ? new ValueAst.Integer(n)
                : new ValueAst.String(raw.ToString()!));
        }
        return new VerbCallAst(
            "core", "wroll", false, [],
            ImmutableDictionary<string, ValueAst>.Empty,
            @params.ToImmutableArray(),
            new Zoh.Runtime.Lexing.TextPosition(1, 1, 0));
    }

    [Fact]
    public void WRoll_NegativeWeight_ReturnsFatal()
    {
        // /wroll "a", 1, "b", -1;  → second weight is negative → fatal invalid_value
        var call = MakeWRollCall("a", 1, "b", -1);
        var result = _driver.Execute(_context, call);
        Assert.False(result.IsSuccess);
        Assert.Equal("invalid_value", result.Diagnostics[0].Code);
    }

    [Fact]
    public void WRoll_NonNumericWeight_ReturnsFatal()
    {
        // /wroll "a", "bad_weight";  → weight is a string → fatal invalid_type
        var call = MakeWRollCall("a", "bad_weight");
        var result = _driver.Execute(_context, call);
        Assert.False(result.IsSuccess);
        Assert.Equal("invalid_type", result.Diagnostics[0].Code);
    }

    [Fact]
    public void WRoll_ValidWeights_ReturnsValue()
    {
        // All weights positive; result must be one of the provided values.
        var call = MakeWRollCall("x", 1, "y", 2);
        var result = _driver.Execute(_context, call);
        Assert.True(result.IsSuccess);
        Assert.Contains(result.Value.AsString().Value, new[] { "x", "y" });
    }
}
```

**Rationale:** Dedicated test file keeps roll-related tests isolated from the broad `CoreVerbTests.cs`. The `MakeWRollCall` helper pushes raw integers or strings into the positional param list using `ValueAst.Integer` / `ValueAst.String` — matching how actual parsed calls look.

> **Note for executor:** Confirm `ValueAst.Integer` exists in the AST (check `ValueAst.cs`). If the node is named differently (e.g. `ValueAst.Int` or `ValueAst.Literal`), adjust accordingly.

---

## Verification Plan

### Automated Checks

```powershell
# Build
cd s:\repos\zoh\c# ; dotnet build

# Run all tests (with output filtered to summary)
cd s:\repos\zoh\c# ; dotnet test | Select-String -Pattern "passed|failed|skipped|error" | Select-Object -Last 5

# Run only the directly affected test classes
cd s:\repos\zoh\c# ; dotnet test --filter "FullyQualifiedName~ParseTests|FullyQualifiedName~RollTests" | Select-String -Pattern "passed|failed|skipped" | Select-Object -Last 5
```

### Acceptance Criteria Validation

| Criterion | How to Verify | Expected Result |
|-----------|---------------|-----------------|
| `/wroll` negative weight → fatal `invalid_value` | `WRoll_NegativeWeight_ReturnsFatal` test | Test passes, `result.IsSuccess == false`, code `invalid_value` |
| `/wroll` non-numeric weight → fatal `invalid_type` | `WRoll_NonNumericWeight_ReturnsFatal` test | Test passes |
| `/parse "[1, \"a\", true]", "list"` succeeds | `Parse_List_FromJson` test | `ZohList` with 3 elements |
| `/parse "{...}", "map"` succeeds | `Parse_Map_FromJson` test | `ZohMap` with correct entries |
| Nested list/map roundtrip | `Parse_NestedStructure` test | Inner `ZohList` has 3 elements |
| Malformed JSON → `invalid_format` | `Parse_MalformedList_ReturnsFatal` test | Fatal with code `invalid_format` |
| All pre-existing tests pass | Full `dotnet test` run | 0 failures |

---

## Rollback Plan

1. Delete `c#/tests/Zoh.Tests/Verbs/Core/RollTests.cs` (new file).
2. Revert `c#/src/Zoh.Runtime/Verbs/Core/RollDriver.cs` to prior commit.
3. Revert `c#/src/Zoh.Runtime/Verbs/Core/ParseDriver.cs` to prior commit.
4. Revert `c#/tests/Zoh.Tests/Verbs/Core/ParseTests.cs` to prior commit.

---

## Notes

### Assumptions
- `System.Text.Json` is available in `Zoh.Runtime` without adding a new package reference (verify at execution time — if missing, add `<PackageReference Include="System.Text.Json" Version="..." />`).
- `ZohList` accepts an `ImmutableArray<ZohValue>` constructor parameter (confirmed in `ZohList.cs`).
- `ZohMap` accepts an `ImmutableDictionary<string, ZohValue>` constructor parameter (confirmed in `ZohMap.cs`).
- `ValueAst.Integer` node type exists for integer literals in the AST (verify at execution time; adjust name if different).
- The `ImmutableDictionary` import is already present in `ParseDriver.cs` (it is not — add `using System.Collections.Immutable;`).

### Risks
- **JSON number ambiguity**: `el.TryGetInt64` is used first; values beyond `Int64` range fall back to double. This matches ZOH's type hierarchy and is acceptable.
- **Key case sensitivity in `ZohMap`**: JSON property names are case-sensitive; `ZohMap` uses ordinal comparison. Tests use exact-case keys to avoid ambiguity.
