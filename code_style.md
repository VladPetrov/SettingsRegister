# code_style.md
.NET code style and engineering standards (mandatory for AI agents).

## Goals
- Produce maintainable, testable, object-oriented .NET code.
- Enforce single responsibility at class and method level.
- Keep behavior deterministic and easy to reason about.
- Minimize hidden coupling and implicit behavior.

## Non-Negotiable Formatting
1. Braces are always required for control flow (`if`, `else`, loops, `lock`, `using`, `switch`).
2. One statement per line.
3. Avoid deep nesting; prefer guard clauses and early returns.
4. File-scoped namespaces are allowed if consistent across the repository.
5. Prefer `var` for locals; use explicit type only when clarity materially improves.

## Naming
- Types: `PascalCase`.
- Methods/properties/events: `PascalCase`.
- Locals/parameters: `camelCase`.
- Private fields: `_camelCase`.
- `const` and `static readonly`: `PascalCase`.
- Interfaces: `I` prefix.
- Avoid non-standard abbreviations (keep `Id`, `Utc`, `Json`, `Dto`).

## Design Requirements
Single responsibility:
- Each class has one primary reason to change.
- Each method does one thing.
- If a method name needs "and", split it.

Cohesion and coupling:
- Keep related behavior together.
- Depend on interfaces.
- Use constructor injection.
- Avoid static mutable state.

Inheritance:
- Prefer composition.
- Use inheritance only for stable is-a relationships with clear invariant ownership.
- Avoid deep hierarchies (target max 2-3 levels).

Immutability:
- Prefer immutable value objects (`record` or `readonly struct`) for data carriers.
- Keep mutable state in explicit stateful domain objects with controlled mutation.

## Layering and API Boundaries
- Keep public surface minimal.
- Default implementation details to `internal`.
- Maintain clean boundaries:
  - Domain: pure rules/invariants, no infrastructure concerns.
  - Application: orchestration/use cases.
  - Infrastructure: IO, persistence, external systems.
  - Presentation: API/UI/adapters.
- Do not leak infrastructure details into domain.

## Validation and Exceptions
- Validate inputs at boundaries (public methods, constructors, deserialization).
- Use guard clauses for invalid arguments.
- Do not use exceptions for normal control flow.
- Throw specific exceptions (`ArgumentNullException`, `ArgumentException`, `InvalidOperationException`).
- Include parameter names and actionable messages without sensitive data leakage.

## Control Flow
- Prefer guard clauses over nested branches.
- Switch expressions are allowed when clearer.
- Avoid boolean flags that significantly alter behavior; prefer strategy abstractions.

## Async and Concurrency
- Async methods end with `Async`.
- Never block on async (`.Result`, `.Wait()` prohibited).
- Pass `CancellationToken` through long-running call chains.
- Protect shared mutable state with explicit synchronization.

## Dependency Injection
- Use constructor injection.
- Do not use service locator patterns.
- Inject narrow interfaces; avoid kitchen-sink dependencies.

## Logging and Observability
- Log at boundaries and decision points, not inside hot loops.
- Use structured logging.
- Avoid logging raw user input unless required; sanitize/truncate when needed.
- Prefer domain events for analytics over ad-hoc logs.

## DTO and Repository Rules
DTOs:
- Allowed only at boundaries (IO, persistence, external APIs).
- Map DTOs to domain objects via explicit mappers/hydrators.
- Do not pass DTOs deep into domain logic.

Repositories:
- Do not duplicate query/filter logic across methods.
- Do not have one public repository method call another public repository method.
- Extract shared private helpers/query builders.

## Refactoring Hygiene
- After refactors, remove dead code in touched files.
- Delete helpers with no call sites in the same change.
- Run build/tests after cleanup to detect regressions.

## Testability
- Add unit tests for non-trivial classes.
- Test pure domain logic without mocks.
- Use mocks only at boundaries/infrastructure.
- Keep tests deterministic and in Arrange-Act-Assert structure.

## Documentation and Comments
- Do not add XML doc comments unless requested.
- Add internal/private comments only when behavior is non-obvious.
- Comments explain why, not what.
- Do not leave TODOs without issue references.

## Method Formatting
- Separate distinct logical blocks with one empty line.
- Do not add empty lines inside tightly coupled statements.
- Prefer short, scannable blocks; extract private methods if methods grow.
- Keep boolean conditions readable with intermediate variables when needed.

## Example Brace Style
Good:
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

Bad:
```csharp
if (isValid) return Result.Success();

foreach (var item in items)
    Process(item);
```

## Prohibited Patterns
- Static mutable state (except narrowly scoped, thread-safe caches).
- Reflection-based hidden auto-wiring without explicit constraints.
- Utility dumping-ground classes.
- Ambiguous method names like `Handle`, `Process`, `DoWork` without domain meaning.
- Methods longer than ~30-40 lines without strong justification.
- Classes longer than ~300-500 lines without strong justification.

## Pull Request Self-Check
- Braces used for all control flow.
- Classes and methods respect SRP.
- Domain layer is infrastructure-free.
- Public APIs remain minimal and documented.
- Tests cover non-trivial behavior.
- Logging is structured and safe.
- No blocking async usage.
