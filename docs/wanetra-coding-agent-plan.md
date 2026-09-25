# Wanetra — Coding Agent Implementation Plan

## 0. Role

You are implementing **Wanetra**, an open-source, self-hosted internet/WAN monitoring application designed primarily for homelab environments.

Your goal is to produce a reliable MVP with a clean architecture, simple deployment, and minimal operational complexity.

Do not over-engineer the project.

The application must run as a single Docker container and support both `linux/amd64` and `linux/arm64`.

---

# 1. Product Definition

Wanetra is a self-hosted application that:

- runs internet speed tests,
- schedules speed tests using cron syntax,
- stores historical measurements,
- visualizes connection metrics,
- detects internet degradation,
- sends degradation and recovery notifications,
- exposes Prometheus metrics,
- runs well on low-resource homelab systems such as Raspberry Pi.

Wanetra should be positioned as:

> A self-hosted internet and WAN health monitoring platform for homelabs.

It is NOT just a speedtest history dashboard.

Core product concept:

```text
Speed Tests
+
Connection Health
+
Degradation Detection
+
Alerts
+
Notifications
+
Prometheus
+
Self-hosted integrations
```

---

# 2. Fixed Technology Stack

Do not replace these technologies unless there is a strong technical reason.

## Frontend

Use:

```text
React
TypeScript
Vite
Tailwind CSS
shadcn/ui
Apache ECharts
TanStack Query
React Router
```

## Backend

Use:

```text
.NET 10
ASP.NET Core Minimal API
Entity Framework Core
SQLite
BackgroundService / IHostedService
Cronos
Serilog
prometheus-net
```

## Infrastructure

Use:

```text
Docker
Docker Compose
GitHub Actions
GitHub Container Registry
```

Supported architectures:

```text
linux/amd64
linux/arm64
```

---

# 3. Architectural Principles

Keep the project modular, but do not introduce unnecessary enterprise abstractions.

Prefer simple code over architecture for architecture's sake.

Do NOT introduce:

```text
Microservices
Redis
RabbitMQ
Kafka
Event sourcing
Complex CQRS
MediatR unless clearly necessary
Kubernetes-specific components
Distributed cache
Separate frontend container
Separate database container
```

The entire MVP should run with:

```text
1 container
1 ASP.NET Core process
1 SQLite database
```

The React frontend must be compiled during the Docker build and served by ASP.NET Core from `wwwroot`.

---

# 4. Repository Structure

Create the repository using this structure:

```text
wanetra/
├── backend/
│   ├── Wanetra.Api/
│   ├── Wanetra.Application/
│   ├── Wanetra.Domain/
│   ├── Wanetra.Infrastructure/
│   └── Wanetra.sln
│
├── frontend/
│   ├── src/
│   ├── public/
│   ├── package.json
│   └── vite.config.ts
│
├── docs/
├── .github/
│   └── workflows/
├── Dockerfile
├── compose.example.yml
├── .env.example
├── README.md
├── CONTRIBUTING.md
├── SECURITY.md
├── LICENSE
└── CHANGELOG.md
```

---

# 5. Backend Project Responsibilities

## Wanetra.Api

Responsible for:

```text
HTTP API
Minimal API endpoints
Middleware
Health checks
Static frontend hosting
Dependency injection
Application bootstrap
```

Do not put core business logic directly in API endpoints.

---

## Wanetra.Application

Responsible for:

```text
Speed test orchestration
Scheduling
Alert evaluation
Baseline calculations
Degradation detection
Notification orchestration
Application services
```

---

## Wanetra.Domain

Responsible for:

```text
Entities
Enums
Value objects where useful
Core interfaces
Domain rules
```

Keep this project lightweight.

---

## Wanetra.Infrastructure

Responsible for:

```text
EF Core
SQLite
Migrations
Speed test engine implementations
Notification provider implementations
Prometheus metrics
External process execution
Logging-related infrastructure
```

---

# 6. Frontend Structure

Use this structure:

```text
frontend/src/
├── api/
├── components/
├── features/
│   ├── dashboard/
│   ├── speedtests/
│   ├── alerts/
│   ├── notifications/
│   └── settings/
├── hooks/
├── layouts/
├── pages/
├── types/
└── main.tsx
```

Avoid a giant shared `components` folder containing unrelated components.

Prefer feature-oriented organization.

---

# 7. Core Domain Models

Implement the following entities.

## SpeedTestResult

Minimum fields:

```text
Id
Timestamp
Engine
DownloadMbps
UploadMbps
LatencyMs
JitterMs
PacketLossPercent
ServerName
ServerLocation
ServerId
Isp
ExternalIp
DurationMs
Success
ErrorMessage
```

Use nullable fields where an engine cannot provide a specific metric.

---

## ScheduleSettings

Minimum fields:

```text
Id
Enabled
CronExpression
Timezone
UpdatedAt
```

Only one schedule is required for MVP.

Design the code so multiple schedules can be added later without requiring a full rewrite.

---

## AlertRule

Minimum fields:

```text
Id
Name
Enabled

MinDownloadMbps
MinUploadMbps
MaxLatencyMs
MaxJitterMs
MaxPacketLossPercent

DownloadBaselineDropPercent
UploadBaselineDropPercent

ConsecutiveFailuresRequired
ConsecutiveRecoveriesRequired

CreatedAt
UpdatedAt
```

All threshold fields should be optional.

---

## DegradationEvent

Minimum fields:

```text
Id
StartedAt
EndedAt
Status
Reason

BaselineDownloadMbps
WorstDownloadMbps

BaselineUploadMbps
WorstUploadMbps

MaxLatencyMs
MaxJitterMs
MaxPacketLossPercent

NotificationSent
RecoveryNotificationSent
```

Possible statuses:

```text
Active
Recovering
Recovered
```

---

## NotificationConfiguration

At minimum support:

```text
Id
Provider
Enabled
ConfigurationJson
CreatedAt
UpdatedAt
```

Provider-specific credentials must never be returned back to the frontend in plaintext after storage.

---

# 8. Speed Test Engine Abstraction

Speed testing must be abstracted.

Create:

```csharp
public interface ISpeedTestEngine
{
    string Name { get; }

    Task<SpeedTestResult> RunAsync(
        CancellationToken cancellationToken);
}
```

Implement at least:

```text
LibreSpeedEngine
```

Future-compatible design:

```text
LibreSpeedEngine
OoklaEngine
CustomEngine
```

The MVP must not depend on Ookla.

---

# 9. LibreSpeed Integration

LibreSpeed is the default engine.

Do not reimplement the actual bandwidth test algorithm inside Wanetra for MVP.

Prefer wrapping an external LibreSpeed-compatible CLI/process.

Responsibilities:

```text
start process
capture stdout
capture stderr
parse output
handle timeout
handle cancellation
map output into SpeedTestResult
```

The process runner must:

- never block indefinitely,
- support cancellation,
- capture failure reason,
- prevent shell injection,
- avoid building commands using unsafe string concatenation.

Implement a dedicated abstraction for external process execution if useful.

---

# 10. Speed Test Concurrency

Only one speed test may run at a time.

This applies to:

```text
manual tests
scheduled tests
future internal triggers
```

Use a backend synchronization mechanism.

Examples:

```text
SemaphoreSlim
or
a dedicated execution coordinator
```

Expected behavior:

```text
test already running
+
scheduled trigger occurs
=
skip scheduled trigger
```

For manual requests, return a meaningful status such as:

```text
409 Conflict
```

or an API result indicating a test is already running.

Never start multiple simultaneous WAN speed tests by default.

---

# 11. Scheduler

Use:

```text
Cronos
+
BackgroundService
```

Do not use Hangfire or another external scheduling framework for MVP.

Requirements:

- standard cron expression support,
- timezone awareness,
- enable/disable,
- next run calculation,
- next several executions preview,
- cron validation,
- no overlapping tests,
- schedule reload when user updates settings,
- graceful cancellation during shutdown.

Default schedule:

```text
*/30 * * * *
```

Meaning:

```text
Every 30 minutes
```

---

# 12. Manual Speed Test

Provide:

```text
POST /api/speedtests/run
```

Do not keep the HTTP request open for the full duration of the speed test.

Preferred behavior:

```json
{
  "status": "started"
}
```

The frontend should then fetch current execution status.

Provide an endpoint such as:

```text
GET /api/speedtests/status
```

Possible states:

```text
idle
running
failed
```

---

# 13. Speed Test API

Implement at minimum:

```text
GET    /api/speedtests
GET    /api/speedtests/latest
GET    /api/speedtests/{id}
POST   /api/speedtests/run
GET    /api/speedtests/status
```

History endpoint must support:

```text
pagination
date range filtering
success/failure filtering
engine filtering
sorting
```

Do not load the entire history table into memory.

---

# 14. Dashboard API

Implement a dashboard-oriented endpoint.

Example:

```text
GET /api/dashboard
```

Return enough information for the main dashboard:

```text
latest measurement
current health state
baseline values
percentage difference from baseline
active degradation event
next scheduled run
recent measurements
```

Avoid forcing the frontend to issue excessive numbers of requests for the first dashboard render.

---

# 15. Baseline Calculation

MVP baseline algorithm:

```text
rolling median
```

Default window:

```text
7 days
```

Calculate separately for:

```text
download
upload
latency if useful
```

Primary baseline comparison should focus on:

```text
download
upload
```

Do not use simple average for speed baseline.

Median is preferred because outliers should have less influence.

---

# 16. Baseline Behavior With Insufficient Data

Do not calculate misleading baselines from too little data.

Introduce a minimum sample requirement.

Example:

```text
minimum 10 successful measurements
```

Until enough measurements exist:

```text
baseline status = unavailable
```

Frontend should show:

```text
Collecting baseline data
```

Do not classify baseline degradation when no reliable baseline exists.

Static threshold alerts may still work.

---

# 17. Alert Evaluation

Support two alert categories.

## Static thresholds

Examples:

```text
Download < 500 Mbps
Upload < 30 Mbps
Latency > 40 ms
Jitter > 15 ms
Packet Loss > 2%
```

## Baseline degradation

Examples:

```text
Download 30% below baseline
Upload 30% below baseline
```

All rule values must be configurable.

---

# 18. Alert State Machine

Do not send an alert immediately after one poor measurement.

Use consecutive test requirements.

Example default:

```text
3 failed tests
→ degraded

2 healthy tests
→ recovered
```

Recommended states:

```text
Healthy
PendingDegradation
Degraded
Recovering
```

Expected transition:

```text
Healthy
    ↓
PendingDegradation
    ↓
Degraded
    ↓
Recovering
    ↓
Healthy
```

If a recovery fails during `Recovering`, return to `Degraded`.

Persist actual degradation incidents as `DegradationEvent`.

---

# 19. Notification Rules

Notifications must only be sent on meaningful state transitions.

Send:

```text
degradation notification
recovery notification
```

Do NOT send repeated degradation messages after every bad speed test.

If notification sending fails:

- log the failure,
- preserve the degradation event,
- do not crash the application.

---

# 20. Notification Provider Abstraction

Create:

```csharp
public interface INotificationProvider
{
    string Name { get; }

    Task SendAsync(
        NotificationMessage message,
        CancellationToken cancellationToken);
}
```

MVP providers:

```text
ntfy
Generic Webhook
```

Future providers:

```text
Gotify
Discord
SMTP
Telegram
```

Provider selection must not be hard-coded into the alert engine.

---

# 21. ntfy

Implement:

```text
Server URL
Topic
Optional username/password or token
Priority
Optional tags
```

Support:

```text
degradation notification
recovery notification
test notification
```

Example degradation content:

```text
Internet degradation detected

Download:
823 → 412 Mbps (-49.9%)

Upload:
47 → 43 Mbps

Latency:
8 → 31 ms
```

---

# 22. Generic Webhook

Allow configuration of:

```text
URL
HTTP method
Headers
Authentication if needed
```

Initial supported events:

```text
connection.degraded
connection.recovered
speedtest.failed
```

Example payload:

```json
{
  "event": "connection.degraded",
  "timestamp": "2026-09-17T15:00:00Z",
  "downloadMbps": 412,
  "baselineDownloadMbps": 823,
  "degradationPercent": 49.9
}
```

Do not expose internal secrets in webhook payloads.

---

# 23. Prometheus Metrics

Expose:

```text
/metrics
```

Implement at least:

```text
wanetra_download_mbps
wanetra_upload_mbps
wanetra_latency_ms
wanetra_jitter_ms
wanetra_packet_loss_percent

wanetra_speedtest_success
wanetra_speedtest_duration_seconds

wanetra_connection_degraded

wanetra_last_speedtest_timestamp

wanetra_speedtests_total
wanetra_speedtest_failures_total
```

Avoid high-cardinality labels.

Do not use:

```text
IP address
server name
error message
timestamp
```

as Prometheus labels.

---

# 24. Health Checks

Provide:

```text
/health
/health/live
/health/ready
```

Checks may include:

```text
application alive
SQLite accessible
critical services initialized
```

Never run an actual internet speed test as part of health checks.

---

# 25. SQLite

Use SQLite by default.

Path:

```text
/data/wanetra.db
```

Use EF Core migrations.

Requirements:

- database automatically initializes,
- migrations run safely at startup,
- SQLite failures produce clear logs,
- database operations use async APIs where reasonable.

Do not require PostgreSQL for MVP.

---

# 26. Data Retention

Add configurable retention.

Default:

```text
365 days
```

Implement a background maintenance job.

Responsibilities:

```text
delete expired speed tests
optionally remove old recovered degradation events later
clean old application logs if managed internally
```

Do not perform retention cleanup on every request.

---

# 27. Frontend Pages

Implement these pages.

## Dashboard

Must show:

```text
Current status
Download
Upload
Latency
Jitter
Packet loss
Last test
Next test
Baseline comparison
Active degradation incident
```

Charts:

```text
Download / Upload
Latency
Jitter
Packet Loss
```

Time filters:

```text
6H
24H
7D
30D
90D
Custom
```

---

## History

Table fields:

```text
Timestamp
Download
Upload
Latency
Jitter
Packet Loss
Server
Engine
Status
```

Features:

```text
pagination
date filtering
sorting
success/error filtering
details view
```

---

## Alerts

Allow the user to configure:

```text
static thresholds
baseline degradation percentage
consecutive failure count
consecutive recovery count
enable/disable
```

---

## Notifications

Allow:

```text
provider configuration
enable/disable
test notification
```

Initial UI:

```text
ntfy
Generic Webhook
```

---

## Settings

Sections:

```text
General
Speed Test
Schedule
Thresholds
Notifications
Data Retention
Integrations
System
```

---

# 28. Frontend Data Fetching

Use:

```text
TanStack Query
```

Use it for:

```text
server state
polling current speed test status
history pagination
settings queries
mutations
cache invalidation
```

Do not duplicate API state into unnecessary global stores.

Avoid Redux unless a genuine need appears.

---

# 29. Charts

Use:

```text
Apache ECharts
```

Charts must support:

```text
responsive resizing
tooltips
zoom where useful
time series
multiple metrics
threshold lines when relevant
```

Do not send hundreds of thousands of raw points to the browser.

For large time ranges, implement backend aggregation/downsampling if needed.

For MVP, simple bounded queries are acceptable.

---

# 30. API Error Format

Return consistent errors.

Recommended shape:

```json
{
  "code": "speedtest_already_running",
  "message": "A speed test is already running."
}
```

Use appropriate HTTP status codes.

Do not return exception stack traces to clients.

---

# 31. Logging

Use:

```text
Serilog
```

Log to:

```text
console
/data/logs/
```

Log important events:

```text
application started
speed test started
speed test completed
speed test failed
schedule executed
schedule skipped
baseline unavailable
degradation pending
degradation detected
recovery detected
notification sent
notification failed
database migration completed
```

Never log:

```text
notification tokens
passwords
authorization headers
webhook secrets
```

---

# 32. Security Requirements

Minimum requirements:

- validate cron syntax,
- validate URLs,
- sanitize and validate external process arguments,
- never expose stored secrets,
- never log secrets,
- prevent multiple concurrent speed tests,
- protect against arbitrary command execution,
- set reasonable API request limits,
- use cancellation tokens,
- run container as non-root,
- avoid unnecessary Linux capabilities.

Authentication is not required for MVP.

Assume users may place Wanetra behind:

```text
Traefik
Nginx
Caddy
Tailscale
VPN
Forward-auth proxy
```

---

# 33. Reverse Proxy Support

Support forwarded headers correctly.

Application must work behind:

```text
Traefik
Nginx
Caddy
```

Do not require HTTPS inside the container.

TLS termination should normally happen at the reverse proxy.

---

# 34. Docker

Use multi-stage build.

Expected stages:

```text
1. Node build
2. .NET build/publish
3. ASP.NET runtime
```

Flow:

```text
frontend
→ npm ci
→ npm run build

backend
→ dotnet restore
→ dotnet publish

frontend dist
→ copied to ASP.NET wwwroot

final runtime image
→ run ASP.NET Core
```

Final container:

- runs as non-root,
- exposes port `8080`,
- stores persistent state under `/data`,
- has a health check if practical.

---

# 35. Docker Compose Example

Provide:

```yaml
services:
  wanetra:
    build: .
    container_name: wanetra
    restart: unless-stopped
    ports:
      - "8080:8080"
    environment:
      - TZ=Europe/Istanbul
      - WANETRA_DATA_PATH=/data
    volumes:
      - wanetra-data:/data

volumes:
  wanetra-data:
```
Build locally using the Docker builder's default platform. Keep architecture
detection in the Dockerfile so the matching LibreSpeed binary is included.


Keep the default deployment simple.

No required external database.

---

# 36. GitHub Actions

Create CI workflow.

Run on:

```text
push
pull_request
```

Backend:

```text
dotnet restore
dotnet build
dotnet test
```

Frontend:

```text
npm ci
npm run lint
npm run build
```

Docker workflow:

```text
build linux/amd64
build linux/arm64
push manifest to GHCR
```

On release/tag, publish:

```text
ghcr.io/USERNAME/wanetra:<version>
ghcr.io/USERNAME/wanetra:latest
```

---

# 37. Tests

Testing should focus on important behavior.

Required unit/integration tests should cover at least:

```text
cron validation
next-run calculation
baseline median calculation
insufficient baseline samples
static threshold evaluation
baseline threshold evaluation
consecutive failure behavior
recovery behavior
alert state transitions
notification dispatch rules
speed-test concurrency lock
```

Do not chase arbitrary 100% coverage.

Prioritize business-critical behavior.

---

# 38. Configuration

Infrastructure-level settings may use environment variables.

Example:

```text
WANETRA_DATA_PATH=/data
WANETRA_PORT=8080
TZ=Europe/Istanbul
```

User-managed application settings should primarily live in SQLite.

Do not require users to edit config files for normal application operation.

---

# 39. Default Values

Use reasonable defaults.

```text
Speed Test Schedule:
Every 30 minutes

Cron:
*/30 * * * *

Baseline Window:
7 days

Minimum Baseline Samples:
10

Download Degradation:
30%

Required Consecutive Failures:
3

Required Consecutive Recoveries:
2

History Retention:
365 days

Notifications:
Disabled by default

Prometheus:
Enabled
```

---

# 40. Development Order

Implement in this order.

## Phase 1 — Bootstrap

1. Create repository structure.
2. Create .NET solution/projects.
3. Create React + Vite project.
4. Configure Tailwind.
5. Configure shadcn/ui.
6. Add Docker development skeleton.
7. Add basic health endpoint.

Acceptance criteria:

```text
backend builds
frontend builds
Docker image builds
React is served by ASP.NET
```

---

## Phase 2 — Persistence

1. Add EF Core.
2. Add SQLite.
3. Create domain entities.
4. Create DbContext.
5. Add migrations.
6. Auto-apply migrations safely.

Acceptance criteria:

```text
application starts with empty /data
database is created
schema is initialized
```

---

## Phase 3 — Speed Test Engine

1. Add `ISpeedTestEngine`.
2. Implement LibreSpeed integration.
3. Add safe process execution.
4. Add timeout/cancellation.
5. Store result.
6. Implement concurrency lock.

Acceptance criteria:

```text
manual speed test runs
result is stored
second simultaneous test is rejected
failed test is recorded safely
```

---

## Phase 4 — Speed Test API

Implement:

```text
POST /api/speedtests/run
GET /api/speedtests/status
GET /api/speedtests/latest
GET /api/speedtests
GET /api/speedtests/{id}
```

Acceptance criteria:

```text
frontend can start test
frontend can monitor test
history can be queried
```

---

## Phase 5 — Scheduler

1. Implement Cronos parsing.
2. Add scheduler BackgroundService.
3. Add timezone handling.
4. Add next-run calculation.
5. Add schedule settings endpoints.
6. Prevent overlapping execution.

Acceptance criteria:

```text
cron schedule can be configured
test runs automatically
schedule can be disabled
next executions are visible
```

---

## Phase 6 — Dashboard

1. Add dashboard API.
2. Add current metric cards.
3. Add charts.
4. Add time range filtering.
5. Add manual speed test button.
6. Add scheduled run information.

Acceptance criteria:

```text
user can understand current WAN state
user can inspect historical speed and latency
```

---

## Phase 7 — Baseline

1. Add successful-result query.
2. Implement request-time rolling median with a fixed 7-day UTC window calculated through `TimeProvider`.
3. Include only results where `Success == true`.
4. Calculate download and upload medians independently.
5. Count valid, non-null samples separately for each metric.
6. Make a metric baseline available only when that metric has at least 10 valid samples.
7. Use the latest successful measurement for current download and upload values.
8. Calculate percent change as `(latest - baseline) / baseline * 100`.
9. Return `null` percent change when the corresponding baseline is unavailable.
10. Expose baseline in the dashboard; show `Collecting baseline data` while unavailable.

Do not add Phase 8 health states, thresholds, alert state machine, or degradation event logic. Do not add a table, migration, or background worker for baseline calculation.

Acceptance criteria:

```text
baseline unavailable before enough metric-specific samples
baseline calculated correctly afterwards from successful results in the UTC window
dashboard shows current vs baseline and percent change
```

---

## Phase 8 — Alert Engine

See `docs/superpowers/specs/2026-09-21-alert-engine-design.md`.

1. Add persistent singleton alert transition state and repository operations.
2. Evaluate only successful persisted results synchronously after persistence.
3. Reuse Phase 7 `BaselineService`; do not duplicate baseline calculation.
4. Evaluate static and baseline conditions with OR semantics; ignore unavailable baseline conditions.
5. Implement consecutive unhealthy/recovery transitions and recovery interruption.
6. Persist one active degradation event and prevent duplicate active events.
7. Define rule-change behavior and reset pending counters when rule identity/version changes.
8. Expose persisted active/recovering event read-only to dashboard.
9. Do not add notification dispatch, background workers, queues, or API-demand evaluation.

Acceptance criteria:

```text
failed speed tests do not affect alert state
one bad successful measurement does not alert
configured number of unhealthy successful measurements creates one degradation event
configured recovery count closes event
recovery interruption returns event to active
pending state survives restart
rule changes have deterministic tested behavior
baseline unavailable does not trigger baseline condition
```

---

## Phase 9 — Notifications

1. Add provider abstraction.
2. Implement ntfy.
3. Implement Generic Webhook.
4. Add configuration UI.
5. Add test notification.
6. Connect provider dispatch to degradation state changes.

Acceptance criteria:

```text
degradation sends one notification
recovery sends one notification
repeated bad tests do not spam notifications
```

---

## Phase 10 — Prometheus

1. Add prometheus-net.
2. Add `/metrics`.
3. Export latest metrics.
4. Add counters.
5. Add health state gauge.

Acceptance criteria:

```text
Prometheus can scrape Wanetra
metrics use stable names
no high-cardinality labels
```

---

## Phase 11 — Retention

1. Add retention setting.
2. Add cleanup background job.
3. Delete expired speed tests.

Acceptance criteria:

```text
old data is removed automatically
current/recent data remains intact
```

---

## Phase 12 — Production Docker

1. Finish multi-stage build.
2. Run container as non-root.
3. Add persistent `/data`.
4. Add arm64 compatibility.
5. Add example Compose.
6. Test on clean deployment.

Acceptance criteria:

```text
docker compose up -d
```

must be enough to start the application.

---

## Phase 13 — CI/CD

1. Add backend CI.
2. Add frontend CI.
3. Add Docker build.
4. Add multi-architecture build.
5. Push images to GHCR.

Acceptance criteria:

```text
PR validates frontend/backend
tag creates Docker image
amd64 and arm64 manifests are published
```

---

# 41. MVP Definition of Done

The MVP is complete only when a user can:

1. Run Wanetra using Docker Compose.
2. Open the React dashboard.
3. Run a manual speed test.
4. View the result.
5. Configure a cron schedule.
6. Automatically collect measurements.
7. View historical download/upload/latency data.
8. Build a 7-day rolling baseline.
9. Configure static and baseline degradation rules.
10. Detect an actual degradation incident.
11. Receive one ntfy degradation notification.
12. Receive one recovery notification.
13. Scrape `/metrics` using Prometheus.
14. Run the same project on amd64 and arm64.

---

# 42. Features Explicitly Out of Scope for MVP

Do NOT implement unless required by an earlier architectural decision:

```text
multi-user system
built-in authentication
OIDC
PostgreSQL
multiple remote agents
multi-WAN
machine learning
mobile app
desktop app
Kubernetes operator
Redis
message broker
distributed scheduling
advanced RBAC
Ookla bundled inside the image
```

---

# 43. Future Compatibility

Do not implement these now, but avoid architecture that blocks them.

Future features may include:

```text
Wanetra remote agents
Multiple WAN interfaces
Multiple schedules
PostgreSQL
OIDC
Gotify
Discord
SMTP
DNS monitoring
HTTP probes
Continuous ping monitoring
Outage detection
Gateway monitoring
ISP changes
External IP history
CSV/JSON export
Server comparison
```

Potential future architecture:

```text
                    Wanetra Server
                           │
             ┌─────────────┼─────────────┐
             │             │             │
             ▼             ▼             ▼
          Home Pi       Office Pi      VPS
          Agent          Agent         Agent
```

---

# 44. Coding Guidelines

General rules:

- prefer readable code,
- keep classes focused,
- avoid unnecessary inheritance,
- favor composition,
- use async APIs,
- propagate `CancellationToken`,
- avoid `.Result` and `.Wait()`,
- avoid global mutable state,
- keep API DTOs separate from EF entities when useful,
- validate user input,
- centralize error handling,
- use structured logging,
- keep secrets out of logs,
- add comments only where logic is not obvious.

Do not create abstractions before they have a real purpose.

---

# 45. Agent Behavior

While implementing:

1. Inspect existing code before changing architecture.
2. Do not rewrite working code without a clear reason.
3. Keep changes incremental.
4. Maintain buildable state.
5. Run relevant tests after significant changes.
6. Run frontend build after frontend changes.
7. Run backend build after backend changes.
8. Update documentation when behavior changes.
9. Do not silently change product requirements.
10. Prefer completing one vertical feature before starting another.

When a requirement is ambiguous, choose the simplest solution consistent with this document.

Do not add speculative complexity.

---

# 46. Final Product Characteristics

The finished MVP should feel like:

```text
simple to deploy
simple to understand
lightweight
homelab-friendly
reliable
observable
integration-friendly
```

A typical user should be able to go from zero to a working deployment with:

```text
docker compose up -d
```

and perform all normal configuration from the web interface.
