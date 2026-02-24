# agents.md
Configuration Change Tracker API - agent operating rules.

## Project Overview
Purpose:
- ASP.NET Core Web API for versioned manifests and manifest-bound configuration instances.
- Immutable change history with critical-change notification based on manifest metadata.
- Deterministic, auditable behavior independent from infrastructure.

Target outcomes:
- Import manifests from JSON as immutable versions.
- Manage config instances linked by `ManifestId`.
- Track add/update/delete as immutable `ConfigChange` history.
- Trigger external monitoring for critical settings.

## Architecture
The solution uses 3 layers:
- Presentation/API: controllers + DTOs.
- Services: business use cases over domain models.
- Data: repositories (+ optional Unit of Work, in-memory persistence).

Rules:
- Repositories map domain <-> persistence.
- Services operate only on domain models.
- Unit of Work (if present) coordinates repository writes.

## Authoritative Sources
The following are mandatory and must stay aligned with implementation:
- `code_style.md` (coding/OOP standards).
- `domain_model.puml` (domain responsibilities and invariants).

## Scope
Allowed:
- Implement/refactor code that conforms to `code_style.md` and `domain_model.puml`.
- Add tests/docs/diagrams consistent with invariants.
- Fix obvious typos in user requests.
- Ask for clarification when intent is ambiguous.

Forbidden:
- Breaking invariants from `domain_model.puml`.
- Silent responsibility changes for core domain objects.
- Guessing ambiguous architectural intent.
- Reverting user edits without explicit confirmation.

## Invariants and Change Policy
If requested changes would violate `domain_model.puml`, stop and ask for explicit confirmation.

If changes affect core domain object attributes/responsibilities/relationships, or manifest versioning/override/critical-notification rules:
- Update `domain_model.puml` and code in the same PR.
- Do not leave partial documentation updates.

Restricted evolution (requires explicit architectural decision + doc updates + migration notes):
- Changes to core responsibilities, invariants, manifest versioning semantics, or override rules.

Allowed evolution:
- New setting metadata fields (without changing root responsibilities).
- New monitoring channels/templates.
- Additional analytics/observability events.

## Coding Standards (Mandatory)
`code_style.md` is enforced, including:
- Braces required for all control flow.
- Constructor injection and strong SRP.
- No utility/manager dumping classes.
- No blocking async calls.
- No infrastructure leakage into domain.
- No XML doc comments unless requested.

## Testing and Regression Safety
For non-trivial changes:
- Add/update unit tests.
- Add/update integration tests for affected API behavior.
- Preserve regression coverage for:
  - Immutable manifest versioning on import.
  - Existing manifest requirement for instance creation.
  - Layer/index validation against manifest constraints.
  - Override allow/deny by manifest permissions.
  - `ConfigChange` add/update/delete validity.
  - Critical notification sourced from manifest setting metadata.

Testing guidance:
- For simple DTO mapping extensions, use minimal focused tests (one per direction is enough when logic is trivial).
- For domain models, test business rules/invariants/state transitions.
- Prefer `Assert.Throws<TException>(() => /* code */)` for exception cases.
- Avoid tests for trivial property plumbing or infrastructure concerns in domain unit tests.

## Ambiguity Rule
When requirements are unclear:
1. Preserve current invariants.
2. Prefer small, reversible changes.
3. Ask for clarification before architectural drift.
