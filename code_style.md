# code_style.md
.NET code style and engineering standards (for AI agent)

This document defines mandatory coding standards for this project. The agent will follow these rules by default.

## goals
- produce maintainable, testable, strongly object-oriented .net code
- enforce single responsibility at class and method level
- keep behavior deterministic and easy to reason about
- minimize hidden coupling and “smart” helpers

## non-negotiable formatting rules
1. braces are always required
   - every `if`, `else`, `for`, `foreach`, `while`, `do`, `lock`, `using`, `switch` statement uses braces, even for one-liners.
   - no single-line statements without braces.

2. one statement per line
   - no comma operator tricks or chained statements on a single line.

3. avoid deeply nested blocks
   - prefer guard clauses and early returns, but still keep braces.

4. file-scoped namespaces are allowed if consistent across the repo
   - choose one approach (file-scoped or block-scoped) and apply everywhere.

5. var usage
   - prefer `var` by default for local variables.
   - use explicit types only when it materially improves readability (very complex/ambiguous expressions) or when required by API shape/signature.

## naming conventions
- types: `PascalCase` (classes, records, structs, enums, interfaces)
- methods/properties/events: `PascalCase`
- locals/parameters: `camelCase`
- private fields: `_camelCase`
- constants: `PascalCase` for `const`, `PascalCase` for `static readonly`
- interfaces: `I` prefix (e.g., `IEscalationCallback`)
- avoid abbreviations unless industry-standard (e.g., `Id`, `Utc`, `Json`, `Dto`)

## object-oriented design requirements

### single responsibility principle (srp)
class-level
- each class will have one primary reason to change.
- avoid “god” services (e.g., `EngineService` that does everything).
- if a class needs multiple collaborators and starts to feel “manager-like”, split responsibilities.

method-level
- each method will do one thing.
- if a method name needs “and”, it is likely violating srp.
- prefer:
  - small methods with clear names
  - composition over flags that change behavior

### cohesion and coupling
- maximize internal cohesion: related operations stay together.
- minimize coupling:
  - depend on interfaces, not concretions
  - inject dependencies (constructor injection)
  - avoid static state

### inheritance policy
- prefer composition over inheritance.
- use inheritance only when:
  - there is a stable is-a relationship
  - base class enforces invariants
  - derived classes override small, well-defined variation points
- avoid deep inheritance hierarchies (target: max 2–3 levels).

### immutability preference
- prefer immutable value objects (`record` or `readonly struct`) for data carriers.
- mutable state lives in explicit state objects (e.g., `CourseProgress`) with controlled mutation.

## api design and layering
- public surface area will be minimal.
- internal implementation details will be `internal` by default.
- define clear boundaries:
  - domain: pure types, rules, invariants; no infrastructure concerns
  - application: orchestration and use cases
  - infrastructure: io, persistence, logging, external services
  - presentation: cli, web, bots, adapters
- never let infrastructure leak into domain (no references to json, http, db, file system in domain types).

## exceptions, validation, and guard clauses
- validate inputs at boundaries (public methods, constructors, deserialization).
- use guard clauses for invalid arguments.
- do not use exceptions for normal control flow.
- throw specific exceptions:
  - `ArgumentNullException`, `ArgumentException`, `InvalidOperationException`
- include parameter names in exceptions.
- error messages will be actionable and not expose sensitive data.

## control flow standards
- guard clauses are preferred to reduce nesting, but braces remain mandatory.
- switch expressions are allowed when they improve clarity.
- avoid boolean flags that significantly change behavior; prefer strategy objects.

## async and concurrency
- async methods end with `Async`.
- do not block on async (`.Result`, `.Wait()` are prohibited).
- use `CancellationToken` for long-running operations and pass it through call chains.
- do not share mutable state across threads without explicit synchronization.

## dependency injection
- use constructor injection.
- do not use service locator patterns.
- do not inject large “kitchen sink” services; inject narrowly-scoped interfaces.

## logging and observability
- log at boundaries and decision points, not inside tight loops.
- log structured data (properties) rather than concatenated strings.
- avoid logging raw user input unless explicitly required; if needed, sanitize or truncate.
- domain events are preferred for analytics over ad-hoc logs.

## data transfer objects (dtos)
- dtos are allowed only at boundaries (io, persistence, external APIs).
- map dtos to domain objects via explicit mappers/hydrators.
- do not pass dtos deep into domain logic.

## repository implementation rules
- avoid duplicating query/filter logic across repository methods.
- do not have one public repository method call another public repository method.
- extract shared private helpers (or private query builders) and reuse them from each public method.

## refactoring hygiene
- after each refactor, scan touched files for dead code and remove unused private methods/helpers.
- if a helper no longer has call sites, delete it in the same change set.
- run build/tests after cleanup to confirm no behavioral regression.

## testability requirements
- every non-trivial class will have unit tests.
- pure domain logic will be tested without mocks.
- use mocks only at boundaries and for infrastructure.
- golden transcript tests will exist for prompt/regression-sensitive flows.
- tests will use arrange-act-assert and be deterministic.

## code documentation rules
- do not add xml doc comments / xml descriptions unless explicitly requested.
- internal/private members only get comments when non-obvious.
- comments explain “why”, not “what”.
- do not leave TODOs without an issue reference.

## examples (mandatory brace style)

good
```csharp
if (isValid)
{
    return Result.Success();
}
else
{
    return Result.Failure("Invalid input.");
}

foreach (var item in items)
{
    Process(item);
}
```
bad
```csharp
if (isValid) return Result.Success();

foreach (var item in items)
    Process(item);
```

### prohibited patterns

static mutable state (except narrowly-scoped caches with explicit thread safety)

hidden magic: reflection-based auto-wiring without clear constraints

“utility” classes with unrelated methods (dumping ground)

ambiguous method names like Handle, Process, DoWork without domain meaning

methods longer than ~30–40 lines without strong justification

classes longer than ~300–500 lines without strong justification

### pull request acceptance checklist (agent will self-check)

- braces are used everywhere for control flow

- each class has a single responsibility and a clear name

- each method does one thing; no “and” methods

- domain is free of infrastructure dependencies

- public APIs are minimal and documented

- unit tests exist for non-trivial behavior

- logging is structured and does not leak sensitive data

- no blocking on async

## Method formatting and logical blocks

- Within a method, separate distinct logical blocks using a single empty line.
- A “logical block” is a contiguous group of statements that accomplish one micro-step, for example:
  - input validation / guard clauses
  - request construction
  - calling external dependencies (IO, network, model calls)
  - processing / normalization / classification
  - decision and branching
  - result construction and return

- Do not insert empty lines inside tightly coupled constructs:
  - inside a single `if` body (unless it contains multiple distinct micro-steps)
  - between a declaration and its immediate use when it would reduce readability

- Prefer extra whitespace over overly long blocks:
  - keep each block short and visually scannable
  - when a method grows, refactor blocks into private methods instead of removing blank lines

- Keep boolean expressions readable:
  - if a condition fits on one line without harming readability, keep it on one line
  - if it becomes dense, split using intermediate variables or multiple lines with clear indentation
