# Settings Register & Change Tracker

Settings Register & Change Tracker is a .NET microservice for managing domain configuration in a controlled and observable way. It allows clients to define configuration structure through manifests, create and modify configuration instances that comply with manifest rules, and record every change as an immutable audit event. Critical updates are detected from domain metadata and propagated to an external monitoring integration through an outbox-driven background process. The service also exposes health checks, metrics, and tracing for operational visibility.

## Assignment Coverage

The solution covers all mandatory requirements and several optional ones, while staying within the time-boxed scope of the exercise.

### Mandatory

- ASP.NET Core Web API on .NET
- Create configuration changes for add, update, and delete operations
- Query change history by time range and other attributes
- Retrieve individual changes by ID
- In-memory persistence, as required by the assignment
- Validation at API and domain boundaries
- Consistent error responses
- Simulated external monitoring integration
- Health check endpoint
- Unit and integration tests

### Optional

- Prometheus-compatible metrics on `/metrics`
- Retry-based asynchronous dispatch for external notifications
- OpenTelemetry tracing and correlation support
- Additional handling around time filtering, concurrency, pagination stability, and domain rule enforcement

## How to Run

### API

Open `SettingsRegister.sln` in Visual Studio, set `SettingsRegister.Api` as the startup project, choose a launch profile, and run the application.

Launch profiles:

- `swagger`
- `http`
- `https`

Typical development URLs:

- `http://localhost:5139`
- `https://localhost:7276`

Useful endpoints:

- `/swagger`
- `/health`
- `/metrics`

### Tests

```bash
dotnet test
```

### Optional observability stack

Start from the repository root:

```bash
docker compose -f .\grafana_loki_tempo_prometeus_config\docker-compose.yml up -d
```

Useful UIs:

- Grafana: `http://localhost:3000`
- Prometheus: `http://localhost:9090`
- Loki: `http://localhost:3100`

Stop it with:

```bash
docker compose -f .\grafana_loki_tempo_prometeus_config\docker-compose.yml down
```

## Architecture Overview

The service is built around four ideas: versioned manifests, mutable configuration instances, immutable change tracking, and asynchronous notification of critical updates.

### Scope

The bounded context includes:

- Manifests: versioned definitions of allowed settings, layers, and override rules
- Configuration instances: mutable runtime state bound to a manifest
- Configuration changes: immutable audit events
- Critical notification intents: asynchronous messages derived from domain metadata

Out of scope for this exercise:

- durable database persistence
- production-grade external transport
- full multi-node production hardening

### Structure

The solution follows a layered architecture, separating API, application, domain, infrastructure, and observability concerns. This keeps business rules independent from HTTP and infrastructure details and makes the code easier to test and evolve.

### Unit of Work

A Unit of Work coordinates related updates as one logical operation. For example, updating a setting may modify the configuration instance, append an audit event, and enqueue an outbox message. These changes belong to one consistency boundary.

### Outbox and background dispatch

Critical updates are not sent directly during request processing. Instead, the service writes a notification intent to an outbox and delivers it asynchronously through `OutboxDispatchService`. This avoids coupling the request path to external availability and supports at-least-once delivery semantics.

The current dispatcher is intentionally simple. A production version would process messages in batches and increase concurrency.

### Locking

The service uses `IDomainLock` where domain invariants are sensitive to concurrency, for example when protecting manifest name + version uniqueness.

Two implementations are considered:

- `InProcessDomainLock` for single-instance deployment
- `DistributedDomainLock` for scaled deployment

### Repository and caching

Persistence is implemented through repositories. A small Decorator-based cache is used in front of repository reads as an infrastructure concern, without leaking caching logic into the domain or application layers.

## Domain Model

The domain model follows a DDD-inspired approach and is centered around four core concepts.

### ManifestDomainRoot

Mutable aggregate responsible for creating and validating manifest definitions. It enforces structural rules such as unique setting keys, valid layer counts, and correct override permissions.

### ManifestValueObject

Immutable representation of manifest rules used by the rest of the domain. It answers questions such as whether a setting exists, whether an override is allowed, and whether a change requires critical notification.

### ConfigurationInstance

Main mutable aggregate for runtime configuration. It is bound to a specific manifest and owns the set of explicit setting values. It enforces mutation rules and computes the effective configuration view, including inherited values across layers.

### ConfigurationChange

Immutable audit event representing a single domain change. It stores the operation type, before and after values, actor, timestamp, and event category.

## Application Services

The application layer orchestrates use cases, coordinates repositories through the Unit of Work, and keeps infrastructure concerns out of the domain model.

### ManifestService

Imports and retrieves manifests. On import, it assigns the next version for a manifest name under a lock, persists the manifest, records a `ManifestImport` audit event, creates an outbox notification intent, and commits through the Unit of Work.

### ConfigurationService

Owns the main configuration workflows: creating instances, reading them, listing them, updating setting values, and deleting instances. During write operations it coordinates locking, delegates validation and mutation to the domain model, records immutable change events, creates outbox messages for critical updates, and commits the full operation.

### ConfigurationChangeQueryService

Provides read access to the immutable change stream, including retrieval by ID and cursor-based listing with filtering.

### OutboxDispatchService

Processes pending outbox messages and delivers them to the monitoring integration. It runs asynchronously, prevents overlapping dispatch cycles, retries failed messages, and updates delivery state.

## Important Technical Decisions

1. Versioned manifests as explicit contracts

Configuration structure is modeled explicitly through versioned manifests rather than being treated as implicit application knowledge. This makes behavior deterministic and traceable.

2. Separation between mutable state and immutable facts

Mutable aggregates handle validation and state transitions, while immutable objects represent rules and history. This keeps invariants explicit and historical data reliable.

3. Immutable audit log

All meaningful mutations produce an append-only `ConfigurationChange`. This improves traceability, even though it makes writes more involved.

4. Unit of Work for consistency

A single business action may update configuration state, audit history, and outbox state. The Unit of Work makes that consistency boundary explicit.

5. Outbox instead of inline notification

Critical changes are stored as notification intents and delivered asynchronously. This keeps the synchronous API path independent from external availability.

6. Explicit domain locking

`IDomainLock` is used where concurrency could break domain invariants, such as manifest version uniqueness or concurrent configuration updates.

7. Lightweight caching as infrastructure

A small decorator-based cache sits in front of repository reads. It is intentionally lightweight and not a central design element.

8. Observability from the start

The service includes health checks, metrics, and tracing because configuration services are operationally sensitive and should be easy to diagnose.

## Limitations and Future Improvements

### Current limitations

- In-memory persistence was required by the assignment, so data is not durable across restarts
- The external monitoring integration is simulated
- `OutboxDispatchService` is intentionally simple and does not yet use batching or higher concurrency
- Full end-to-end multi-node behavior is not implemented
- Production-grade outbox guarantees would require durable persistence

### Next improvements

- Replace in-memory repositories with durable storage
- Persist the outbox in transactional storage
- Improve dispatcher throughput with batching and controlled parallelism
- Add full distributed coordination where needed
- Expand dashboards and alerting around critical change flows
- Settings must support versioning

## Metrics

### Repository Cache Metrics

The cached repository decorators emit `System.Diagnostics.Metrics` instruments for cache efficiency and read latency on `GetByIdAsync`.

| Metric | Type | Unit | Tags | Business value |
| --- | --- | --- | --- | --- |
| `SettingsRegister.repository.cache_hit_total` | Counter | count | `repo` (`manifest`, `configuration`, `configuration_change`) | Confirms caching is reducing inner repository load and improving read performance. |
| `SettingsRegister.repository.cache_miss_total` | Counter | count | `repo` (`manifest`, `configuration`, `configuration_change`) | Early signal of increased inner repository load (often preceding latency regressions). |
| `SettingsRegister.repository.get_by_id_duration_ms` | Histogram | ms | `repo` (`manifest`, `configuration`, `configuration_change`), `source` (`cache`, `inner`) | Quantifies read-by-id latency and isolates whether time is spent in cache lookup or inner repository. |

### Service Metrics

These metrics align to the primary outcomes: immutable audit stream, manifest versioning/import correctness, and reliable critical notification delivery via outbox.

| Metric | Counters / instruments | Formula | Business value |
| --- | --- | --- | --- |
| Critical Change Delivery SLO (%) (within `T`) | `SettingsRegister.outbox.critical_created_total`, `SettingsRegister.outbox.critical_sent_total`, `SettingsRegister.outbox.critical_delivery_duration_ms` (histogram) | `SLO% = (count(delivery_duration_ms <= T) / critical_created_total) * 100` | Validates that critical changes are delivered to monitoring fast enough to prevent incidents. |
| Critical notification success rate (%) | `SettingsRegister.outbox.critical_created_total`, `SettingsRegister.outbox.critical_sent_total` | `success% = (critical_sent_total / critical_created_total) * 100` | Confirms critical-change notifications are actually delivered (not just written to outbox). |
| Outbox add rate vs deliver rate (msg/min) | `SettingsRegister.outbox.message_created_total`, `SettingsRegister.outbox.message_sent_total` | `add_rate = rate(created_total)`, `deliver_rate = rate(sent_total)`, `net_growth = add_rate - deliver_rate` | Shows whether dispatch keeps up with production load (growth vs drain) without tracking backlog directly. |
| Outbox dispatch failure rate (%) | `SettingsRegister.outbox.dispatch_attempt_total`, `SettingsRegister.outbox.dispatch_failed_total` | `failure% = (dispatch_failed_total / dispatch_attempt_total) * 100` | Detects notifier/transport failures early and protects alert reliability. |
| Manifest import conflict rate (%) | `SettingsRegister.manifest.import_attempt_total`, `SettingsRegister.manifest.import_conflict_total` | `conflict% = (import_conflict_total / import_attempt_total) * 100` | Signals contention/versioning workflow issues that block safe configuration publishing. |
| Config write latency (p95/p99) | `SettingsRegister.api.request_duration_ms` (histogram, tags: `route`, `method`, `status`), `SettingsRegister.api.request_total` (counter) | `p95/p99(request_duration_ms{route=\"/api/config-instances/{id}/cells\",method=\"PUT\"})` | Protects operator and automation SLAs for rollouts and emergency response changes. |
| Change query latency (p95/p99) | `SettingsRegister.api.request_duration_ms` (histogram, tags: `route`, `method`, `status`), `SettingsRegister.api.request_total` (counter) | `p95/p99(request_duration_ms{route=\"/api/config-changes\",method=\"GET\"})` | Keeps audit/troubleshooting workflows responsive (compliance and investigations). |
| API reliability (5xx rate, availability) | `SettingsRegister.api.request_total` (counter, tags: `route`, `method`, `status`) | `5xx_rate = rate(request_total{status=~\"5..\"}) / rate(request_total)`; `availability = 1 - 5xx_rate` | Measures service trustworthiness and catches regressions/infrastructure failures quickly. |
| Critical-change mix (%) | `SettingsRegister.change.total`, `SettingsRegister.change.critical_total` | `critical_mix% = (critical_total / total) * 100` | Forecasts notification load and highlights high-risk/noisy rollout periods. |

## Error Contract

Errors return `ProblemDetails` with consistent status mapping:

- `400` invalid request payload/model state
- `404` resource not found
- `409` conflict (uniqueness collisions)
- `501` feature not implemented for current environment
- `422` domain validation failures
- `500` unexpected internal failures
