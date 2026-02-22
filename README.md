# Configuration Change Tracker API

ASP.NET Core Web API for tracking configuration rule changes in a Back-Office system.

This project is focused on clarity, strong layering, and auditability. It tracks add/update/delete operations for domain-specific rule configuration (for example credit limits and approval policies), supports query and retrieval, and notifies an external monitoring service for critical changes.

## Scenario

You joined the Back-Office team and need to deliver a configuration change tracker that:
- logs configuration changes,
- stores and retrieves history,
- notifies an external monitoring service when critical changes happen.

## Scope (Task 1.3)

Mandatory:
- .NET implementation
- REST API with at least:
  - create new config change (add/update/delete)
  - list changes by time or type
  - retrieve specific change by ID
- in-memory persistence (no database)
- input validation with clear error handling
- one simulated external integration (notification or logging)
- health check endpoint
- unit and integration tests
- README with rationale, assumptions, and run instructions

Optional bonus:
- metrics on `/metrics`
- retry logic or circuit breaker for external call
- tracing with correlation ID
- edge case handling (for example invalid state transitions)

## Design Rationale

- Auditability first: changes are immutable historical records.
- Domain safety: invariants around change shape and operation type are enforced in domain/application logic.
- Infrastructure isolation: external monitoring integration is behind an interface and simulated in infrastructure.
- Simplicity: in-memory storage keeps the assignment focused on API design and business behavior.

## Assumptions

- "Configuration change" is modeled as an immutable event-like record.
- Operation types are `Add`, `Update`, `Delete`.
- Critical notification is determined from `ConfigDefinition` (not from the change payload).
- Time filtering uses UTC timestamps.
- In-memory persistence resets when the process restarts.

## API Endpoints (Minimum Contract)

- `POST /api/config-changes`
  - Creates a config change record.
  - Supports `Add`, `Update`, `Delete` semantics.

- `GET /api/config-changes`
  - Lists changes.
  - Supports filtering by:
    - `type` (rule type/category)
    - time window (`fromUtc`, `toUtc`)

- `GET /api/config-changes/{id}`
  - Retrieves a change by identifier.

- `GET /health`
  - Health check endpoint.

Bonus endpoint if implemented:
- `GET /metrics`

## Example Request (Create)

```json
{
  "ruleType": "CreditLimit",
  "operation": "Update",
  "targetKey": "CustomerSegment:SMB",
  "beforeValue": "50000",
  "afterValue": "75000",
  "reason": "Risk policy revision",
  "changedBy": "backoffice.user@company.com"
}
```

## Validation and Error Handling

Expected behavior:
- `400 Bad Request` for invalid payload or operation/value mismatch.
- `404 Not Found` when a change ID does not exist.
- `422 Unprocessable Entity` (or `409 Conflict`) for domain rule violations.
- `500 Internal Server Error` only for unexpected failures.

Problem Details responses should be used for consistency.

## External Monitoring Integration (Simulated)

Critical changes should trigger a call to an external monitoring client interface, for example:
- `IMonitoringNotifier.NotifyCriticalChangeAsync(...)`

In the seed implementation this integration is simulated (stub/fake/no-op with logging), but remains asynchronous and failure-aware.

## Suggested Architecture

3-tier structure:
- API layer: controllers, request/response DTOs, HTTP validation concerns.
- Service layer: use-case orchestration, domain rule enforcement, integration orchestration.
- Data layer: in-memory repository and optional Unit of Work abstraction.

See `domain_model.puml` for domain responsibilities and invariants.

## How To Run

1. Restore and build:
```bash
dotnet restore
dotnet build
```

2. Run API project (replace path with actual API project):
```bash
dotnet run --project BackOfficeSmall.Api
```

3. Open Swagger/UI (if enabled by project template):
- `https://localhost:<port>/swagger`

4. Check health endpoint:
```bash
curl https://localhost:<port>/health
```

## How To Test

```bash
dotnet test
```

Testing expectations:
- Unit tests for domain validation and service behavior.
- Integration tests for endpoint contracts and error responses.

## Authoritative Documents

- `code_style.md` - mandatory coding and design standards
- `domain_model.puml` - domain model and invariants
- `AGENTS.md` - agent behavior and architecture guardrails
