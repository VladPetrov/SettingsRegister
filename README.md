# Configuration Change Tracker API

ASP.NET Core Web API for importing versioned manifests, creating manifest-bound configuration instances, tracking immutable config changes, and delivering critical-change notifications through an outbox pipeline.

## Architecture

The solution is strict-layered:

1. `SettingsRegister.Domain`
   - Manifest split:
     - `ManifestDomainRoot` (write-side root with controlled mutation APIs + `Validate()`)
     - `ManifestValueObject` (immutable read-side behavior object with `HasSetting`, `RequiresCriticalNotification`, `CanOverride`)
- Aggregate root: `ConfigurationInstance`
   - Supporting domain types: `ManifestSettingDefinition`, `ManifestOverridePermission`, `SettingCell`, `ConfigurationChange`, `ConfigurationOperation`
   - Domain contracts: `IManifestRepository`, `IConfigurationRepository`, `IConfigurationChangeRepository`, `IMonitoringNotifierOutboxRepository`, `IMonitoringNotifier`
2. `SettingsRegister.Application`
   - Use-case orchestration services:
     - `ManifestService` (import + retrieval by id/list)
     - `ConfigurationInstanceService` (instance CRUD + cell mutation + outbox intent writes for critical changes)
     - `ConfigurationChangeQueryService` (query by id and filters)
     - `OutboxDispatchService` (background polling + one-shot outbox dispatch)
     - `AuthExchangeService` (development token exchange endpoint behavior)
   - Application contracts/requests and application exceptions
3. `SettingsRegister.Infrastructure`
   - In-memory repository implementations with thread-safety
   - Repository decorators with in-memory caching:
     - `CachedManifestRepository`
     - `CachedConfigurationRepository`
   - Persistence model `ManifestEntity` (no `Validate()`)
   - Hydration component `ManifestValueObjectHydrator` for mapping entity -> value object
   - In-memory outbox persistence for notification intents
   - Simulated monitoring transport (`SimulatedMonitoringNotifier`) with send success/failure return
4. `SettingsRegister.Api`
   - REST controllers, DTOs, mapping, validation, `ProblemDetails` error middleware, `/health`
   - `ManifestFileDto` JSON contract for file deserialization (structure only; no file-import workflow yet)
5. `SettingsRegister.Tests`
   - Unit tests for domain invariants and service decisions
   - Integration tests for core endpoints and error contracts

## Core Domain Rules

- Manifest write-side state is represented by `ManifestDomainRoot` with read-only collection exposure and explicit mutation methods.
- Manifest behavior checks are performed through immutable `ManifestValueObject`.
- `ConfigurationInstance` embeds `ManifestValueObject` and enforces manifest-bound cell mutation rules in-domain.
- Manifest uniqueness: (`Name`, `Version`).
- Configuration instance name is unique.
- Configuration instance must reference an existing `ManifestId`.
- Cells are unique per (`ConfigurationId`, `SettingKey`, `LayerIndex`).
- Layer index must stay in `0..LayerCount-1`.
- Setting key must exist in the instance manifest.
- Override is allowed only when instance manifest permission for (`SettingKey`, `LayerIndex`) allows it.
- `ConfigurationChange` is immutable and validates operation semantics:
  - `Add`: `AfterValue` required, `BeforeValue` absent
  - `Update`: both values required
  - `Delete`: `BeforeValue` required, `AfterValue` absent
- `ConfigurationChange` includes `EventType` to separate `ConfigurationSetting` and `ManifestImport` events in a single change stream.
- `ManifestImport` events are always critical and emit outbox notification intent.
- Critical notification is derived from manifest setting definition metadata.
- Critical notification delivery uses transactional outbox semantics: configuration write, `ConfigurationChange`, and outbox intent are committed together.

## Assumptions

- Persistence is in-memory only.
- In `Development`, startup seeds in-memory storage from JSON files:
  - `SettingsRegister.Api/SeedData/manifests.seed.json`
  - `SettingsRegister.Api/SeedData/configuration.seed.json`
- Monitoring transport is simulated and at-least-once; delivery reliability is handled by outbox retries.
- Application runtime settings are bound from `appsettings*.json` into immutable `ApplicationSettings` and `AuthSettings`, then registered in DI.
- `Application:AppScaling` controls lock strategy:
  - `false` (default): `InProcessDomainLock`
  - `true`: simulated `DistributedDomainLock` (placeholder for DB-backed distributed lock in real deployments)
- `Application:ManifestImportLockTimeoutSeconds` configures manifest import lock acquisition timeout (default `30` seconds).
- `Application:ManifestCacheExpirationSeconds` configures local in-memory sliding expiration for manifest `GetByIdAsync` caching (default `300` seconds).
- `Application:ConfigurationCacheExpirationSeconds` configures local in-memory sliding expiration for configuration instance `GetByIdAsync` caching (default `300` seconds).
- `Application:ConfigurationChangeCacheExpirationSeconds` configures local in-memory sliding expiration for configuration change `GetByIdAsync` caching (default `300` seconds).
- Configuration instance cache isolation uses `ConfigurationInstance.Clone()` so callers never mutate cached references.
- `/api/auth/exchange` is enabled in `Development` only; in non-development environments it returns `501 Not Implemented`.
- UTC is required for date filters (`fromUtc`, `toUtc`) when provided.
- API does not expose domain entities directly; DTO mapping is explicit.

## API Endpoints

### Manifest
- `POST /api/manifests/import`
- `GET /api/manifests` (returns all manifests without pagination; fields: `manifestId`, `name`, `version`, `createdAtUtc`)
  - Optional query filter: `?name=<manifest-name>` for exact name match (case-insensitive)
- `GET /api/manifests/{manifestId}`

### Configuration Instance
- `POST /api/config-instances`
- `GET /api/config-instances`
- `GET /api/config-instances/{instanceId}`
- `DELETE /api/config-instances/{instanceId}`
- `PUT /api/config-instances/{instanceId}/cells`

### Configuration Change
- `GET /api/config-changes?pageSize=<1..200>&cursor=<opaque-cursor>&operation=<Add|Update|Delete>&settingKey=<setting-or-manifest-name>&eventType=<ConfigurationSetting|ManifestImport>&fromUtc=<utc-z>&toUtc=<utc-z>`
  - `pageSize` is optional; default is `50`.
  - `cursor` is optional for the first page.
  - `settingKey` is optional; exact match, case-insensitive.
  - `eventType` is optional.
  - Returns `{ items: [...], nextCursor: "..." }`.
- `GET /api/config-changes/{id}`

### Auth
- `POST /api/auth/exchange`
  - `Development`: returns a JWT token payload (`accessToken`, `tokenType`, `expiresAtUtc`)
  - Non-development: returns `501` `ProblemDetails`

### Operational
- `GET /health`
  - Includes probes for:
    - Manifest repository
    - Configuration repository
    - Configuration change repository
    - Monitoring outbox repository
    - Monitoring notifier availability
  - Repository probe failures are treated as critical and return overall `Unhealthy` (`503`).
  - Monitoring notifier availability probe failures return `Degraded` unless another critical probe is unhealthy.

## Observability

### Tracing

The API configures OpenTelemetry tracing for incoming ASP.NET Core requests and application-level service spans emitted from `SettingsRegister.Application`.

- Tracing source name: `SettingsRegister.Application`
- Default behavior: tracing pipeline is enabled, exporter is inactive until endpoint is configured.
- Configure exporter endpoint with:
  - `Application:TracingEnabled` (`true` / `false`)
  - `Application:TracingOtlpEndpoint` (for example `http://localhost:4317`)

Example:

```json
{
  "Application": {
    "TracingEnabled": true,
    "TracingOtlpEndpoint": "http://localhost:4317"
  }
}
```

### Metrics

### Repository Cache Metrics

The cached repository decorators emit `System.Diagnostics.Metrics` instruments for cache efficiency and read latency on `GetByIdAsync`.

Metric | Type | Unit | Tags | Business value
---|---|---|---|---
`SettingsRegister.repository.cache_hit_total` | Counter | count | `repo` (`manifest`, `configuration`, `configuration_change`) | Confirms caching is reducing inner repository load and improving read performance.
`SettingsRegister.repository.cache_miss_total` | Counter | count | `repo` (`manifest`, `configuration`, `configuration_change`) | Early signal of increased inner repository load (often preceding latency regressions).
`SettingsRegister.repository.get_by_id_duration_ms` | Histogram | ms | `repo` (`manifest`, `configuration`, `configuration_change`), `source` (`cache`, `inner`) | Quantifies read-by-id latency and isolates whether time is spent in cache lookup or inner repository.

### Service Metrics

These metrics align to the primary outcomes: immutable audit stream, manifest versioning/import correctness, and reliable critical notification delivery via outbox.

Metric | Counters / instruments | Formula | Business value
---|---|---|---
Critical Change Delivery SLO (%) (within `T`) | `SettingsRegister.outbox.critical_created_total`, `SettingsRegister.outbox.critical_sent_total`, `SettingsRegister.outbox.critical_delivery_duration_ms` (histogram) | `SLO% = (count(delivery_duration_ms <= T) / critical_created_total) * 100` | Validates that critical changes are delivered to monitoring fast enough to prevent incidents.
Critical notification success rate (%) | `SettingsRegister.outbox.critical_created_total`, `SettingsRegister.outbox.critical_sent_total` | `success% = (critical_sent_total / critical_created_total) * 100` | Confirms critical-change notifications are actually delivered (not just written to outbox).
Outbox add rate vs deliver rate (msg/min) | `SettingsRegister.outbox.message_created_total`, `SettingsRegister.outbox.message_sent_total` | `add_rate = rate(created_total)`, `deliver_rate = rate(sent_total)`, `net_growth = add_rate - deliver_rate` | Shows whether dispatch keeps up with production load (growth vs drain) without tracking backlog directly.
Outbox dispatch failure rate (%) | `SettingsRegister.outbox.dispatch_attempt_total`, `SettingsRegister.outbox.dispatch_failed_total` | `failure% = (dispatch_failed_total / dispatch_attempt_total) * 100` | Detects notifier/transport failures early and protects alert reliability.
Manifest import conflict rate (%) | `SettingsRegister.manifest.import_attempt_total`, `SettingsRegister.manifest.import_conflict_total` | `conflict% = (import_conflict_total / import_attempt_total) * 100` | Signals contention/versioning workflow issues that block safe configuration publishing.
Config write latency (p95/p99) | `SettingsRegister.api.request_duration_ms` (histogram, tags: `route`, `method`, `status`), `SettingsRegister.api.request_total` (counter) | `p95/p99(request_duration_ms{route=\"/api/config-instances/{id}/cells\",method=\"PUT\"})` | Protects operator and automation SLAs for rollouts and emergency response changes.
Change query latency (p95/p99) | `SettingsRegister.api.request_duration_ms` (histogram, tags: `route`, `method`, `status`), `SettingsRegister.api.request_total` (counter) | `p95/p99(request_duration_ms{route=\"/api/config-changes\",method=\"GET\"})` | Keeps audit/troubleshooting workflows responsive (compliance and investigations).
API reliability (5xx rate, availability) | `SettingsRegister.api.request_total` (counter, tags: `route`, `method`, `status`) | `5xx_rate = rate(request_total{status=~\"5..\"}) / rate(request_total)`; `availability = 1 - 5xx_rate` | Measures service trustworthiness and catches regressions/infrastructure failures quickly.
Critical-change mix (%) | `SettingsRegister.change.total`, `SettingsRegister.change.critical_total` | `critical_mix% = (critical_total / total) * 100` | Forecasts notification load and highlights high-risk/noisy rollout periods.

## Error Contract

Errors return `ProblemDetails` with consistent status mapping:

- `400` invalid request payload/model state
- `404` resource not found
- `409` conflict (uniqueness collisions)
- `501` feature not implemented for current environment
- `422` domain validation failures
- `500` unexpected internal failures

## Auth Configuration

```json
{
  "Auth": {
    "DevSigningKey": "dev-only-signing-key-change-before-production",
    "Issuer": "SettingsRegister",
    "Audience": "SettingsRegister.Api",
    "TokenLifetimeMinutes": 15
  }
}
```

## Run

```bash
dotnet restore
dotnet build SettingsRegister.sln
dotnet run --project SettingsRegister.Api/SettingsRegister.Api.csproj
```

## Test

```bash
dotnet test SettingsRegister.sln
```

## Design Rationale

- Domain model is explicit and invariant-driven for auditability.
- Manifest responsibilities are separated between domain root, value object, persistence entity, and file DTO.
- Application services orchestrate use cases while domain objects enforce their own invariants.
- In-memory repositories enforce uniqueness constraints at boundary entry points.
- Critical notification decision is centralized in mutation flow and derived from manifest metadata, not caller flags.
- Outbox + dispatcher split ensures write consistency and resilient eventual delivery without inline external calls.

