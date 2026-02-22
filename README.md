# Configuration Change Tracker API

ASP.NET Core Web API for tracking configuration changes in a hierarchical settings table.

## Scenario

You join the Back-Office team and build a configuration change tracker that:
- imports and versions table manifests,
- stores configuration instances bound to a manifest version,
- records add/update/delete cell changes,
- notifies an external monitoring service for critical updates.

## Final Domain Design

The domain is a hierarchical settings table (not a tree).

- Manifest defines table structure and size.
- ConfigInstance stores actual values for one table instance.
- ConfigChange stores immutable audit history of changes.

### Aggregate roots

1. Manifest
2. ConfigInstance

### Manifest

Manifest is immutable after creation.

Required shape:
- `ManifestId`
- `Name`
- `Version`
- `LayerCount`
- setting definitions (columns)
- override permissions per (`SettingKey`, `LayerIndex`)

Versioning rules:
- Importing JSON creates a new manifest version for a name.
- Unique key is (`Name`, `Version`).

### ConfigInstance

ConfigInstance references one manifest by `ManifestId`.

Required shape:
- `ConfigInstanceId`
- unique `Name`
- `ManifestId`
- value cells keyed by (`SettingKey`, `LayerIndex`)

Binding rule:
- Instance is pinned to the referenced manifest version.

### ConfigChange

Immutable change log record per value operation:
- add/update/delete operation
- before/after values
- actor and UTC timestamp
- target cell (`SettingKey`, `LayerIndex`) and instance reference

Criticality rule:
- Critical notification is derived from manifest/setting definition metadata.
- It is not a mutable flag on `ConfigChange` payload.

## Core Invariants

- Manifest is immutable once created.
- (`Name`, `Version`) for Manifest is unique.
- ConfigInstance name is unique.
- ConfigInstance must reference an existing ManifestId.
- Layer index must be within `0..LayerCount-1`.
- Setting key must exist in the referenced manifest.
- Override at (`SettingKey`, `LayerIndex`) is allowed only when manifest permits it.
- No duplicate value entry for the same (`ConfigInstanceId`, `SettingKey`, `LayerIndex`).

## API Scope

Minimum assignment endpoints remain:
- `POST /api/config-changes`
- `GET /api/config-changes`
- `GET /api/config-changes/{id}`
- `GET /health`

Domain-focused endpoints expected by this design:
- manifest import/versioning endpoints
- config instance CRUD endpoints
- instance value mutation endpoints with invariant validation

## Technical Focus

Mandatory:
- .NET
- REST API
- in-memory persistence
- input validation and clear error handling
- one simulated external integration
- health check endpoint
- unit and integration tests

Optional:
- `/metrics`
- retry/circuit-breaker for external call
- correlation ID tracing
- additional edge case handling

## How To Run

```bash
dotnet restore
dotnet build
dotnet run --project BackOfficeSmall.Api
```

## How To Test

```bash
dotnet test
```

## Authoritative Documents

- `code_style.md` - mandatory coding and design standards
- `domain_model.puml` - domain model and invariants
- `AGENTS.md` - agent behavior and architecture guardrails
