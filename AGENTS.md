# agents.md
Configuration Change Tracker API - Agent Guidelines and Responsibilities

This document defines how AI agents operate within the Configuration Change Tracker API project.

---

## project overview

### purpose
This project implements an ASP.NET Core Web API for managing versioned manifests and configuration instances represented as hierarchical settings tables.

Core design principles and invariants are defined in `domain_model.puml`.

The system is intentionally engineered so that rules are auditable, deterministic, and independent from infrastructure details.

### target outcomes
- import manifests as JSON and create immutable versions
- create and manage config instances bound to a manifest version (`ManifestId`)
- track add/update/delete value changes as immutable history
- notify an external monitoring service for critical changes derived from manifest setting metadata
- keep behavior predictable for audits and operations

---

## architecture

The solution follows a 3-tier application design:
- Presentation/API layer (controllers + DTOs)
- Service layer (business logic using domain models)
- Data layer (repositories + optional Unit of Work + in-memory persistence)

Repository and mapping rules:
- Repositories map between domain models and persistence entities/records.
- Services operate only on domain models.
- Unit of Work (if present) coordinates repository changes.

---

## authoritative documents (must be kept in sync)

1. `code_style.md`
   Mandatory .NET coding standards and OOP / SRP requirements.

2. `domain_model.puml`
   UML representation of domain objects, responsibilities, and invariants.

These documents define the contract between architecture, implementation, and agent behavior.

---

## agent scope and responsibility

### what the agent is allowed to do
- generate new code that conforms to:
  - `code_style.md`
  - `domain_model.puml`
- refactor existing code to better align with the defined architecture
- add functionality only when it does not violate frozen invariants
- generate tests, documentation, and diagrams within the same constraints
- fix obvious typos in user requests
- ask for clarification when user intent is ambiguous

### what the agent must not do
- violate constraints or invariants defined in `domain_model.puml`
- silently change responsibilities of core domain objects
- make assumptions about ambiguous requirements without clarification
- revert user code edits without explicit confirmation

---

## architectural invariants (non-negotiable)

Architectural invariants are defined in `domain_model.puml`.

If a requested change would violate an invariant, the agent must stop and request explicit confirmation.

---

## change management rules (critical)

### mandatory documentation synchronization
If any of the following change:
- attributes of a core domain object
- responsibilities of a core domain object
- relationships between core domain objects
- decision rules for manifest versioning, override permissions, or critical notifications

then all of the following must be updated in the same pull request:
- `domain_model.puml`
- affected code

Partial updates are not allowed.

### pull request checklist (agent-enforced)
Before completing a pull request, the agent will verify:
- code complies with `code_style.md`
- domain responsibilities match `domain_model.puml`
- UML diagram still accurately reflects implementation
- no invariant has been violated
- public APIs remain minimal and documented
- tests cover new or changed behavior

If the checklist fails, the agent must fix issues before completion.

---

## coding standards enforcement

The agent treats `code_style.md` as mandatory, not advisory.

Key enforcement points:
- braces are required everywhere
- strong OOP with constructor injection
- single responsibility at class and method level
- no "utility" or "manager" dumping grounds
- no blocking async
- no infrastructure leakage into domain
- no XML descriptions/doc comments unless explicitly requested

Any generated code that violates these rules is invalid.

---

## domain evolution policy

### allowed evolution
- adding new settings metadata fields without changing root responsibilities
- adding new monitoring channels/templates
- extending analytics/observability events

### restricted evolution
Changes that alter responsibilities, invariants, manifest versioning semantics, or override rules defined in `domain_model.puml` require:
1. explicit architectural decision
2. updates to all authoritative documents
3. clear migration notes in the pull request

---

## agent behavior in ambiguous situations

When requirements are ambiguous, the agent will:
1. preserve existing invariants
2. prefer smaller, reversible changes
3. ask for clarification before introducing architectural drift

The agent will never guess architectural intent.

---

## testing and regression safety

The agent will:
- add or update unit tests for any non-trivial logic
- add or update integration tests for API endpoints
- ensure regression coverage for core decisions:
  - manifest import creates new version and keeps previous versions immutable
  - instance must reference existing manifest id
  - layer/index validation against manifest constraints
  - override allowed/denied based on manifest permissions
  - add/update/delete operation validity for `ConfigChange`
  - critical notification decision sourced from manifest setting definition

### testing strategies

#### simplified mapping test strategy
For straightforward DTO mapping extensions:
- use minimal focused tests for key property mapping
- avoid exhaustive edge case testing when mapping is trivial
- one test per mapping direction is sufficient when logic is simple

#### domain model test strategy
For domain aggregates/entities:
- focus on business rules and state transitions
- test domain invariants and constraints
- test conditional update logic where applicable
- use `Assert.Throws<TException>(() => /* code */)` for exception testing

Examples of what not to test:
- constructor parameter validation only
- trivial property getters/setters
- infrastructure behavior in domain unit tests

---

## summary

This project is architecture-first.

The agent's primary responsibility is architectural integrity.

If code, documentation, and diagrams diverge, the agent must realign them before completion.
