# Configuration Change Tracker API

ASP.NET Core Web API for importing versioned manifests, creating manifest-bound configuration instances, tracking immutable config changes, and notifying a simulated external monitor for critical changes.

## Architecture

The solution is strict-layered:

1. `BackOfficeSmall.Domain`
   - Manifest split:
     - `ManifestDomainRoot` (mutable write-side object, `Validate()` only)
     - `ManifestValueObject` (immutable read-side behavior object with `HasSetting`, `RequiresCriticalNotification`, `CanOverride`)
   - Aggregate root: `ConfigInstance`
   - Supporting domain types: `ManifestSettingDefinition`, `ManifestOverridePermission`, `SettingCell`, `ConfigChange`, `ConfigOperation`
   - Domain contracts: `IManifestRepository`, `IConfigInstanceRepository`, `IConfigChangeRepository`, `IMonitoringNotifier`
2. `BackOfficeSmall.Application`
   - Use-case orchestration services:
     - `ManifestService` (import/version lookup)
     - `ConfigInstanceService` (instance CRUD + cell mutation)
     - `ConfigChangeQueryService` (query by id and filters)
   - Application contracts/requests and application exceptions
3. `BackOfficeSmall.Infrastructure`
   - In-memory repository implementations with thread-safety
   - Persistence model `ManifestEntity` (no `Validate()`)
   - Hydration component `ManifestValueObjectHydrator` for mapping entity -> value object
   - Simulated async monitoring notifier (`SimulatedMonitoringNotifier`)
4. `BackOfficeSmall.Api`
   - REST controllers, DTOs, mapping, validation, `ProblemDetails` error middleware, `/health`
   - `ManifestFileDto` JSON contract for file deserialization (structure only; no file-import workflow yet)
5. `BackOfficeSmall.Tests`
   - Unit tests for domain invariants and service decisions
   - Integration tests for core endpoints and error contracts

## Core Domain Rules

- Manifest write-side state is represented by mutable `ManifestDomainRoot`.
- Manifest behavior checks are performed through immutable `ManifestValueObject`.
- Manifest uniqueness: (`Name`, `Version`).
- Config instance name is unique.
- Config instance must reference an existing `ManifestId`.
- Cells are unique per (`ConfigInstanceId`, `SettingKey`, `LayerIndex`).
- Layer index must stay in `0..LayerCount-1`.
- Setting key must exist in the referenced manifest.
- Override is allowed only when manifest permission for (`SettingKey`, `LayerIndex`) allows it.
- `ConfigChange` is immutable and validates operation semantics:
  - `Add`: `AfterValue` required, `BeforeValue` absent
  - `Update`: both values required
  - `Delete`: `BeforeValue` required, `AfterValue` absent
- `ConfigChange` references `ConfigInstanceId`; manifest context is derived from the instance.
- Critical notification is derived from manifest setting definition metadata.

## Assumptions

- Persistence is in-memory only.
- Monitoring integration is simulated, async, injectable, and retry-free.
- UTC is required for date filters (`fromUtc`, `toUtc`) when provided.
- API does not expose domain entities directly; DTO mapping is explicit.

## API Endpoints

### Manifest
- `POST /api/manifests/import`
- `GET /api/manifests/{manifestId}`
- `GET /api/manifests/latest/{name}`

### Config Instance
- `POST /api/config-instances`
- `GET /api/config-instances`
- `GET /api/config-instances/{instanceId}`
- `DELETE /api/config-instances/{instanceId}`
- `PUT /api/config-instances/{instanceId}/cells`

### Config Change
- `POST /api/config-changes`
- `GET /api/config-changes`
- `GET /api/config-changes/{id}`

### Operational
- `GET /health`

## Error Contract

Errors return `ProblemDetails` with consistent status mapping:

- `400` invalid request payload/model state
- `404` resource not found
- `409` conflict (uniqueness collisions)
- `422` domain validation failures
- `500` unexpected internal failures

## Run

```bash
dotnet restore
dotnet build BackOfficeSmall.sln
dotnet run --project BackOfficeSmall.Api
```

## Test

```bash
dotnet test BackOfficeSmall.sln
```

## Design Rationale

- Domain model is explicit and invariant-driven for auditability.
- Manifest responsibilities are separated between domain root, value object, persistence entity, and file DTO.
- Application services orchestrate rules using only domain interfaces.
- In-memory repositories enforce uniqueness constraints at boundary entry points.
- Critical notification decision is centralized in mutation flow and derived from manifest metadata, not caller flags.
- Documentation (`README.md`, `domain_model.puml`) is intentionally kept aligned with implementation.
