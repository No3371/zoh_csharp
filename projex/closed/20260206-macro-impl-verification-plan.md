# Plan: Macro Implementation Verification

> **Status:** Complete
> **Completed:** 2026-02-06
> **Walkthrough:** [closed/20260206-macro-impl-verification-walkthrough.md](./closed/20260206-macro-impl-verification-walkthrough.md)
> **Author:** Agent
> **Source:** [proposal-macro-redo.md](../../projects/proposal-macro-redo.md)
> **Related Projex:** 
> - [projects/20260206-macro-spec-verification-plan.md](../../projects/20260206-macro-spec-verification-plan.md) (spec)
> - [20260206-proposal-core-002-macro-pipe-syntax-plan-impl.md](./closed/20260206-proposal-core-002-macro-pipe-syntax-plan-impl.md) (prior impl)
> **Reviewed:** 2026-02-06 - [Review Report](./20260206-macro-impl-verification-plan-review.md)
> **Review Outcome:** Approved

---

## Summary

Verifies and corrects C# runtime macro implementation (`MacroPreprocessor.cs`) to fully cover the original proposal design. Adds missing tests for untested features.

**Scope:** `MacroPreprocessor.cs`, `PreprocessorTests.cs`
**Estimated Changes:** 2 files modified

---

## Objective

### Problem / Gap / Need

Current implementation has gaps relative to proposal:

1. **Relative placeholders verification** — Verify `|%+N|`, `|%-N|` logic (review suggests it works, need tests)
2. **Indentation preservation** — Not implemented
3. **Symmetric trimming** — Not implemented
4. **Missing arg behavior** — Uses empty string (may differ from final spec)
5. **Missing tests** — No tests for relative placeholders, indentation

### Success Criteria

- [ ] Relative placeholder `|%+N|` works correctly
- [ ] Relative placeholder `|%-N|` works correctly
- [ ] Indentation is preserved on expansion
- [ ] Arguments are trimmed symmetrically (`min(leading, trailing)`)
- [ ] Escaping: `\%` is unescaped to `%`
- [ ] Tests exist and pass for all features
- [ ] All existing tests still pass

### Out of Scope

- Spec document updates (separate projex)
- `\%` escaping (pending spec decision)

---

## Context

### Current Implementation Issues

**File:** `c#/src/Zoh.Runtime/Preprocessing/MacroPreprocessor.cs`

#### Issue 1: Relative Placeholders (Verification)

Original plan suspected a bug here, but review suggests `int.TryParse` handles signs correctly. We need to **verify** this with tests rather than assume it's broken.

```csharp
// Current code:
else if (int.TryParse(content, out int idx))
{
    if (content.StartsWith("+") || content.StartsWith("-"))
        targetIdx = autoIndex + idx;  // idx includes sign (+2 or -2)
    else
        targetIdx = idx;
}
```

**Verification:**
- `|%+2|`: `idx=2`. `targetIdx = auto + 2`. CORRECT.
- `|%-2|`: `idx=-2`. `targetIdx = auto - 2`. CORRECT.

**Action:** Add tests to confirm this behavior.

#### Issue 2: Indentation Not Implemented

No indentation detection or application in `ExpandMacros` method.

#### Issue 3: Standard vs Symmetric Trimming

Original plan proposed `Trim()`, but spec requires "Symmetric Trimming" (remove `min(lead, trail)` from both ends).

#### Issue 4: Escaping

Spec requires `\%` to be treated as literal `%` and unescaped in arguments. Current `ParseArgs` only handles `\|`.

### Key Files

| File | Purpose | Changes Needed |
|------|---------|----------------|
| `src/Zoh.Runtime/Preprocessing/MacroPreprocessor.cs` | Preprocessor | Add indentation, symmetric trimming, `\%` unescaping |
| `tests/Zoh.Tests/Preprocessing/PreprocessorTests.cs` | Tests | Add tests for relative, indentation, symmetric trimming, escaping |

---

## Implementation

### Step 1: Confirm Relative Placeholders Work (Test First)

**File:** `tests/Zoh.Tests/Preprocessing/PreprocessorTests.cs`

**Changes:** Add tests to verify current behavior:

```csharp
[Fact]
public void Macro_Expands_RelativeForward()
{
    var processor = new MacroPreprocessor();
    var source = @"
|%REL%|
/log ""|%|"", ""|%+1|"";
|%REL%|

|%REL|A|B|C|%|
";
    var context = new PreprocessorContext(source, "/test.zoh");
    var result = processor.Process(context);

    // |%| = A (auto=0, then auto=1)
    // |%+1| = args[1+1] = args[2] = C
    Assert.Contains("/log \"A\", \"C\";", result.ProcessedText);
}

[Fact]
public void Macro_Expands_RelativeBackward()
{
    var processor = new MacroPreprocessor();
    var source = @"
|%REL%|
/log ""|%|"", ""|%|"", ""|%-1|"";
|%REL%|

|%REL|A|B|C|%|
";
    var context = new PreprocessorContext(source, "/test.zoh");
    var result = processor.Process(context);

    // |%| = A (auto=0→1), |%| = B (auto=1→2), |%-1| = args[2-1] = args[1] = B
    Assert.Contains("/log \"A\", \"B\", \"B\";", result.ProcessedText);
}
```

---

### Step 2: Implement Indentation Preservation

**File:** `src/Zoh.Runtime/Preprocessing/MacroPreprocessor.cs`

**Changes:** In `ExpandMacros` method, detect and apply indentation:

1. When finding `|%NAME|`, scan backward to line start to capture leading whitespace
2. After expanding body, prepend whitespace to each line (except first, which replaces the call)

**Pseudocode:**
```csharp
// Find indentation before |%NAME|
int lineStart = source.LastIndexOf('\n', openIdx) + 1;
string indent = "";
for (int j = lineStart; j < openIdx && char.IsWhiteSpace(source[j]); j++)
    indent += source[j];

// Apply indent to expanded body
string expanded = ExpandBody(macro.Body, args);
if (!string.IsNullOrEmpty(indent))
{
    expanded = expanded.Replace("\n", "\n" + indent);
}
```

---

### Step 3: Implement Symmetric Trimming and Escaping

**File:** `src/Zoh.Runtime/Preprocessing/MacroPreprocessor.cs`

**Changes:** In `ParseArgs` method:

1.  **Escaping:** Add handling for `\%` -> `%`.
2.  **Symmetric Trimming:** Apply the `min(leading, trailing)` removal logic to the final argument.

**Pseudocode:**
```csharp
// Inside ParseArgs loop, handle \%
else if (argsContent[i] == '\\' && i + 1 < argsContent.Length && argsContent[i + 1] == '%')
{
    currentArg.Append('%'); // Unescape \% -> %
    i++;
}

// ... logic ...

// At end, use helper for symmetric trimming
args.Add(SymmetricTrim(currentArg.ToString()));

// Helper
private string SymmetricTrim(string input)
{
    if (string.IsNullOrEmpty(input)) return input;
    
    int leading = 0;
    while (leading < input.Length && char.IsWhiteSpace(input[leading])) leading++;
    
    // All whitespace -> Empty
    if (leading == input.Length) return "";
    
    int trailing = 0;
    while (trailing < input.Length && char.IsWhiteSpace(input[input.Length - 1 - trailing])) trailing++;
    
    int toRemove = Math.Min(leading, trailing);
    
    return input.Substring(toRemove, input.Length - (toRemove * 2));
}
```

---

### Step 4: Add Indentation Test

**File:** `tests/Zoh.Tests/Preprocessing/PreprocessorTests.cs`

```csharp
[Fact]
public void Macro_PreservesIndentation()
{
    var processor = new MacroPreprocessor();
    var source = @"
|%BLOCK%|
/if *x,
    /log ""yes"";
;
|%BLOCK%|

    |%BLOCK|%|
";
    var context = new PreprocessorContext(source, "/test.zoh");
    var result = processor.Process(context);

    // Expanded lines should have 4-space indent
    Assert.Contains("    /if *x,", result.ProcessedText);
    Assert.Contains("        /log \"yes\";", result.ProcessedText);
}
```

---

### Step 5: Add Symmetric Trimming and Escaping Tests

**File:** `tests/Zoh.Tests/Preprocessing/PreprocessorTests.cs`

```csharp
[Fact]
public void Macro_SymmetricTrim_Basic()
{
    var processor = new MacroPreprocessor();
    // "  A  " (2,2) -> "A"
    // " A " (1,1) -> "A"
    // " A  " (1,2) -> "A "
    // "  A " (2,1) -> " A"
    var source = @"
|%T%|/v ""|%0|"";|%T%|

|%T|  A  |%|
|%T| A |%|
|%T| A  |%|
|%T|  A |%|
";
    var context = new PreprocessorContext(source, "/test.zoh");
    var result = processor.Process(context);

    Assert.Contains("/v \"A\";", result.ProcessedText);
    Assert.Contains("/v \"A \";", result.ProcessedText);
    Assert.Contains("/v \" A\";", result.ProcessedText);
}

[Fact]
public void Macro_Escaping_Percent()
{
    var processor = new MacroPreprocessor();
    var source = @"
|%E%|/v ""|%0|"";|%E%|

|%E| \% |%|
|%E| 100\% |%|
";
    var context = new PreprocessorContext(source, "/test.zoh");
    var result = processor.Process(context);

    Assert.Contains("/v \"%\";", result.ProcessedText);
    Assert.Contains("/v \"100%\";", result.ProcessedText);
}
```

---

## Verification Plan

### Automated Tests

- **Command:** `cd c# && dotnet test --filter "PreprocessorTests"`
- **Expected:** All tests pass (existing 7 + new 8 = 15 tests)

### Test Coverage

| Feature | Test Name | Status |
|---------|-----------|--------|
| No-args expansion | `Macro_DefinesAndExpands_NoArgs` | ✅ Exists |
| Positional args | `Macro_Expands_PositionalArgs` | ✅ Exists |
| Auto-increment | `Macro_Expands_AutoIncArgs` | ✅ Exists |
| Missing args | `Macro_HandleMissingArg_AsEmptyString` | ✅ Exists |
| Escaped pipes | `Macro_HandlesEscapedPipes` | ✅ Exists |
| Multiline args | `Macro_HandlesMultilineArgs` | ✅ Exists |
| **Relative forward** | `Macro_Expands_RelativeForward` | ❌ Add (Verify) |
| **Relative backward** | `Macro_Expands_RelativeBackward` | ❌ Add (Verify) |
| **Indentation** | `Macro_PreservesIndentation` | ❌ Add |
| **Symmetric trimming** | `Macro_SymmetricTrim_Basic` | ❌ Add |
| **Escaping %** | `Macro_Escaping_Percent` | ❌ Add |

---

## Rollback Plan

- Revert changes: `git checkout -- src/ tests/`

---

## Notes

### Dependencies

- **Blocked by:** Spec verification plan must decide missing arg behavior first
- **Related:** If spec changes missing arg to `?`, update test `Macro_HandleMissingArg_AsEmptyString`

### Risks

- Indentation change might affect existing scripts
- Trimming could alter whitespace-sensitive content

### Open Questions

- [ ] Confirm missing arg → empty string (pending spec decision)
