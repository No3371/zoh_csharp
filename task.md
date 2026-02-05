# ZOH C# Runtime Implementation

## Overview
Building a complete ZOH runtime in C# from the ground up, following the language spec and implementation workflow.

---

## Phase 1: Foundation (Lexer, Parser, Preprocessor)

### 1.1 Project Setup
- [x] Create C# solution structure (Zoh.Runtime project)
- [x] Set up test project (Zoh.Tests)
- [x] Configure .NET 8+ target, nullable reference types
- [ ] Set up git repository with proper .gitignore

### 1.2 Lexer Implementation
- [x] Define `TokenType` enum (all 50+ token types)
- [x] Create `Token` record/class with position tracking
- [x] Implement `Position` struct (line, column, offset)
- [x] Create `Scanner` class with character stream handling
- [x] Implement basic token scanning (punctuation, operators)
- [x] Implement string lexing (single/double quotes, multiline)
- [x] Implement number lexing (integers, doubles)
- [x] Implement identifier/keyword lexing
- [x] Implement comment skipping (inline `::`, multiline `:::`)
- [x] Implement expression backtick lexing
- [x] Implement syntactic sugar tokens (`=====>`, `====+`, `<===+`)
- [x] Implement preprocessor directive tokens (`#embed`, `#macro`)
- [x] Add escape sequence handling
- [x] Create comprehensive spec compliance tests for Parser
- [x] Fix Lexer.AddToken ArgumentOutOfRangeException
- [x] Fix Lexer Interpolation Sugar ($ "...")
- [x] Implement Block Verb parsing in Parser
- [x] Ensure Parser handles empty statements / trailing semicolons
- [x] Implement Type Coercion rules in ValueExtensions
- [x] Verify all core verbs against spec (CoreVerbTests)(including compliance tests)
- [x] Write comprehensive lexer tests (including compliance tests)

### 2.1 Rigorous Codebase Review (Current Phase)
- [x] Review `Parser.cs` against `impl/02_parser.md`
    - [x] `ParseStory` & Metadata (Fixed greediness)
    - [x] `ParseStatement` dispatch & Sugar handling (Fixed Lexer/Flag defects)
    - [x] `ParseVerbCall` (Block vs Line, Adjacency check)
    - [x] Parameter parsing (Comma rules vs Whitespace)
    - [x] `ParseValue` & Recursion
- [x] Review `ExpressionParser.cs` against `impl/04_expressions.md`
    - [x] Operator precedence
    - [x] Special forms (`$()`, `$?()`, etc.)
    - [x] Literal & Reference parsing
- [x] Review Tests against Spec
    - [x] `ParserSpecComplianceTests.cs` (Updated and Verified)
    - [x] `ExpressionTests.cs` (Verified with `ExpressionParserComplianceTests`)
- [x] Fix identified discrepancies
- [ ] Verify `Lexer.cs` specific edge cases

### 2.2 Core Runtime Features (Paused)
- [x] Define AST node types (StoryNode, VerbCallNode, etc.)
- [x] Create Value hierarchy (NothingValue, BoolValue, IntegerValue, etc.)
- [x] Implement `Parser` class with token navigation
- [x] Implement story header parsing (name, metadata)
- [x] Implement label parsing
- [x] Implement verb call parsing (standard and block forms)
- [x] Implement attribute parsing
- [x] Implement parameter parsing (named and unnamed)
- [x] Implement value parsing (all types)
- [x] Implement reference parsing with index support
- [x] Implement collection parsing (lists, maps)
- [x] Implement sugar statement transformation (set, get, capture, jump, etc.)
- [x] Implement error recovery/synchronization
- [x] Write comprehensive parser tests

### 1.4 Preprocessor Implementation
- [x] Create `Preprocessor` interface and priority system
- [x] Implement `EmbedPreprocessor` for `#embed` directives
- [x] Implement cycle detection for embedded files
- [x] Implement `MacroPreprocessor` for `#macro`/`#expand`
- [x] Implement placeholder parsing and expansion
- [x] Create `SourceMap` for error location mapping
- [x] Write preprocessor tests

---

## Phase 2: Core Execution (Types, Expressions, Core Verbs)

### 2.1 Type System
- [x] Implement `Value` base class/interface
- [x] Implement `NothingValue` (singleton pattern)
- [x] Implement `BoolValue`
- [x] Implement `IntegerValue` (64-bit)
- [x] Implement `DoubleValue` (64-bit IEEE 754)
- [x] Implement `StringValue` (UTF-8)
- [x] Implement `ListValue` with all operations
- [x] Implement `MapValue` with all operations
- [x] Implement `ChannelValue`
- [x] Implement `VerbValue` (objectified verb call)
- [x] Implement `ExpressionValue`
- [x] Implement `ReferenceValue` with resolution
- [x] Implement type coercion rules
- [x] Implement truthiness checks
- [x] Write type system tests

### 2.2 Variable Storage
- [x] Implement `Scope` enum
- [x] Implement `Variable` record with type constraint info
- [x] Implement `VariableStore` dicts (Global/Local)
- [x] Implement `VariableStore` get/set logic with shadowing
- [x] Implement `VariableStore` type constraint enforcement
- [x] Write variable storage tests

### 2.3 Expressions
- [x] Create `ExpressionLexer` for expression tokenization
- [x] Create `ExpressionParser` (Recursive Descent with precedence)
- [x] Implement AST for literals, unary, binary, grouping, variables
- [x] Implement `ExpressionEvaluator` visitor/switch
- [x] Implement arithmetic operators (`+`, `-`, `*`, `/`, `%`)
- [x] Implement comparison operators (`==`, `!=`, `<`, `>`, `<=`, `>=`)
- [x] Implement logical operators (`&&`, `||`)
- [x] Implement string concatenation
- [x] Implement variable resolution in expressions
- [x] Implement type coercion in expressions
- [x] Write expression evaluation tests

### 2.4 Core Verbs
- [x] Create `VerbDriver` interface and `ExecutionResult`
- [x] Create helper methods (ok, info, warning, error, fatal)
- [x] Implement `SetDriver` (Core.Set)
- [x] Implement `GetDriver` (Core.Get)
- [x] Implement `DropDriver` (Core.Drop)
- [x] Implement `CaptureDriver` (Core.Capture)
- [x] Implement `DiagnoseDriver` (Core.Diagnose)
- [x] Implement `InterpolateDriver` (Core.Interpolate)
- [x] Implement `EvaluateDriver` (Core.Evaluate)
- [x] Implement `TypeDriver` (Core.Type)
- [x] Implement `CountDriver` (Core.Count)
- [x] Implement `DoDriver` (Core.Do)
- [x] Implement `IncreaseDriver` / `DecreaseDriver`
- [x] Implement `RollDriver` / `WRollDriver` / `RandDriver`
- [x] Implement `ParseDriver`
- [x] Implement `DeferDriver`
- [x] Implement debug verbs (info, warning, error, fatal)
- [x] Implement `HasDriver`, `AnyDriver`, `FirstDriver`
- [x] Implement collection verbs (Append, Insert, Remove, Clear)
- [x] Write core verb tests

---

## Phase 3: Control Flow & Concurrency

### 3.1 Control Flow Verbs
- [x] Implement `IfDriver` with else support
- [x] Implement `SwitchDriver` with default case
- [x] Implement `LoopDriver`
- [x] Implement `WhileDriver`
- [x] Implement `ForeachDriver`
- [x] Implement `SequenceDriver`
- [x] Write control flow tests

### 3.2 Concurrency & Navigation
- [ ] Create `Context` class with execution state
- [ ] Implement instruction pointer management
- [ ] Implement `JumpDriver` for label navigation
- [ ] Implement `ForkDriver` for parallel contexts
- [ ] Implement `CallDriver` for blocking fork
- [ ] Implement `ExitDriver` for context termination
- [ ] Implement `SleepDriver` with timer
- [ ] Implement `InlineDriver` for variable merging
- [ ] Write navigation tests

### 3.3 Channel System
- [ ] Create `ChannelManager` class
- [ ] Implement channel creation and lifecycle
- [ ] Implement `PushDriver` for channel send
- [ ] Implement `PullDriver` with timeout support
- [ ] Implement `CloseDriver` for channel cleanup
- [ ] Implement channel generation tokens
- [ ] Write channel tests

### 3.4 Signal System
- [ ] Create `SignalManager` class
- [ ] Implement `WaitDriver` for message waiting
- [ ] Implement `SignalDriver` for broadcasting
- [ ] Implement timeout handling
- [ ] Write signal tests

---

## Phase 4: Runtime Architecture

### 4.1 Runtime Core
- [ ] Create `Runtime` class as top-level coordinator
- [ ] Implement handler registry (preprocessors, compilers, validators, drivers)
- [ ] Implement story compilation pipeline
- [ ] Implement context management (create, run, terminate)
- [ ] Implement execution loop with blocking support
- [ ] Implement tick-based scheduler for blocked contexts
- [ ] Implement defer execution (story and context defers)
- [ ] Write runtime tests

### 4.2 Compilation Pipeline
- [ ] Create `CompiledStory` structure
- [ ] Create `CompiledVerbCall` structure
- [ ] Create `Compiler` interface
- [ ] Implement default compiler for AST conversion
- [ ] Implement verb validators
- [ ] Implement story validators
- [ ] Write compilation tests

### 4.3 Standard Verbs
- [ ] Implement `ConverseDriver` (presentation)
- [ ] Implement `ChooseDriver` with multiple selection
- [ ] Implement `PromptDriver` for user input
- [ ] Implement `ShowDriver` / `HideDriver` for visuals
- [ ] Implement `PlayDriver` / `StopDriver` / `PauseDriver` for audio
- [ ] Implement `FlagDriver` for runtime flags
- [ ] Write standard verb tests

### 4.4 Persistent Storage
- [ ] Create `PersistentStorage` interface
- [ ] Implement default file-based storage
- [ ] Implement `WriteDriver` (Store.Write)
- [ ] Implement `ReadDriver` (Store.Read)
- [ ] Implement `EraseDriver` (Store.Erase)
- [ ] Implement `PurgeDriver` (Store.Purge)
- [ ] Implement store containers
- [ ] Write storage tests

### 4.5 Validation Pipeline
- [ ] Implement `DiagnosticLevel` enum
- [ ] Implement `Diagnostic` class
- [ ] Implement label validation (undefined, duplicate)
- [ ] Implement required parameter validation
- [ ] Implement type validation where applicable
- [ ] Write validation tests

---

## Phase 5: Integration & Polish

### 5.1 Integration Testing
- [ ] Implement test runner for `.zoh` files
- [ ] Create integration test suite from 13_testing.md scenarios
- [ ] Test compilation pipeline end-to-end
- [ ] Test concurrency scenarios
- [ ] Test storage persistence
- [ ] Test error handling and diagnostics

### 5.2 Documentation
- [ ] Document public API
- [ ] Create usage examples
- [ ] Document extension points (custom verbs, handlers)

### 5.3 Performance & Polish
- [ ] Profile and optimize hot paths
- [ ] Add logging infrastructure
- [ ] Review error messages for clarity
- [ ] Final code review and cleanup

---

## Notes & Findings

Use repomem to log questions, findings, and feedback for spec improvements.
