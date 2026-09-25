# Wanetra - Project Plan

> Project name: **Wanetra**  
> Goal: Build a lightweight, self-hosted internet/WAN monitoring application for homelab environments.

---

## 1. Project Goal

Wanetra will periodically test a user's internet connection, store historical metrics, visualize trends in a dashboard, detect connection degradation, and send notifications through self-hosted or external notification providers.

The application should be:

- Self-hosted
- Open source
- Docker-first
- Easy to deploy
- Lightweight enough for Raspberry Pi / small homelab servers
- Multi-architecture (`amd64`, `arm64`)
- Usable without external SaaS dependencies
- Prometheus/Grafana friendly

The initial application will be deployed as a **single Docker container**.

---

## 2. Core Use Cases

The application should allow a user to:

- Run internet speed tests manually
- Schedule automatic speed tests
- Store historical results
- View download/upload speed trends
- Monitor latency
- Monitor jitter
- Monitor packet loss
- Detect internet performance degradation
- Configure alert thresholds
- Receive degradation/recovery notifications
- Export current metrics to Prometheus
- Review previous degradation events
- Configure everything from a web UI

---

## 3. Technology Stack

### Frontend

- React
- TypeScript
- Vite
- Tailwind CSS
- shadcn/ui
- Apache ECharts
- TanStack Query
- React Router

### Backend

- ASP.NET Core
- .NET 10
- Minimal API
- BackgroundService / IHostedService
- Entity Framework Core
- SQLite
- Cronos
- Serilog
- prometheus-net

### Infrastructure

- Docker
- Docker Compose
- GitHub Actions
- GitHub Container Registry
- Multi-architecture images
  - `linux/amd64`
  - `linux/arm64`

---

# 4. High-Level Architecture

```text
                    Browser
                       │
                       ▼
               ┌──────────────┐
               │ React Web UI │
               └──────┬───────┘
                      │ REST
                      ▼
              ┌─────────────────┐
              │ ASP.NET Core API│
              └────────┬────────┘
                       │
        ┌──────────────┼──────────────┐
        │              │              │
        ▼              ▼              ▼
 Scheduler       Test Runner     Alert Engine
        │              │              │
        │              ▼              ▼
        │       Speedtest Engines Notification Manager
        │
        ▼
   SQLite Database

ASP.NET Core
    │
    └── /metrics
          │
          ▼
      Prometheus
          │
          ▼
       Grafana
```

The React frontend should be built during the Docker image build and served directly by ASP.NET Core.

No separate frontend container should be required.

---

# 5. Docker Deployment

Target deployment:

```yaml
services:
  wanetra:
    build: .
    container_name: wanetra
    restart: unless-stopped
    ports:
      - "8080:8080"
    volumes:
      - ./data:/data
```

Application data:

```text
/data/
├── wanetra.db
├── logs/
└── config/
```

Configuration should primarily be stored in the database, while environment variables should be used for infrastructure-level settings.

Example:

```env
WANETRA_DATA_PATH=/data
WANETRA_PORT=8080
TZ=Europe/Istanbul
```

---

# 6. Backend Project Structure

Proposed structure:

```text
backend/
├── Wanetra.Api/
│   ├── Endpoints/
│   ├── Middleware/
│   └── Program.cs
│
├── Wanetra.Application/
│   ├── SpeedTests/
│   ├── Scheduling/
│   ├── Alerts/
│   ├── Notifications/
│   └── Services/
│
├── Wanetra.Domain/
│   ├── Entities/
│   ├── Enums/
│   ├── Events/
│   └── Interfaces/
│
├── Wanetra.Infrastructure/
│   ├── Persistence/
│   ├── SpeedTestEngines/
│   ├── Notifications/
│   ├── Metrics/
│   └── Logging/
│
└── Wanetra.sln
```

The project should avoid unnecessary enterprise abstractions.

Clean separation is useful, but the architecture should remain practical for a small self-hosted application.

---

# 7. Frontend Project Structure

```text
frontend/
├── src/
│   ├── api/
│   ├── components/
│   ├── features/
│   │   ├── dashboard/
│   │   ├── speedtests/
│   │   ├── alerts/
│   │   ├── notifications/
│   │   └── settings/
│   ├── hooks/
│   ├── layouts/
│   ├── pages/
│   └── types/
│
├── public/
└── package.json
```

---

# 8. Speed Test Engine

Speed testing must be abstracted from the application.

Interface concept:

```csharp
public interface ISpeedTestEngine
{
    string Name { get; }

    Task<SpeedTestResult> RunAsync(
        CancellationToken cancellationToken);
}
```

Initial engines:

```text
ISpeedTestEngine
├── LibreSpeedEngine
├── OoklaEngine
└── CustomEngine
```

## LibreSpeed

LibreSpeed should be the default supported engine.

Metrics:

- Download Mbps
- Upload Mbps
- Ping
- Jitter
- Packet loss if available
- Selected server
- Test duration

## Ookla

Ookla support should be optional.

The application should not depend on Ookla being installed.

Possible configuration:

```text
Speed Test Engine
○ LibreSpeed
○ Ookla
```

If Ookla is selected, the user should provide/install the CLI binary separately if required by licensing/distribution constraints.

---

# 9. Speed Test Result Model

Example entity:

```text
SpeedTestResult

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

Not every engine must provide every field.

Nullable fields should be used where appropriate.

---

# 10. Scheduler

Speed tests should support both simple and advanced scheduling.

## Simple scheduling

Examples:

```text
Every 15 minutes
Every 30 minutes
Every hour
Every 6 hours
Daily
```

## Advanced scheduling

Cron syntax:

```text
*/30 * * * *
```

Use:

```text
Cronos
+
BackgroundService
```

The application should calculate the next execution time itself.

Scheduler requirements:

- Timezone aware
- Prevent overlapping speed tests
- Manual test should not accidentally start multiple concurrent tests
- Scheduler can be enabled/disabled
- Cron validation
- Show next run time
- Show next several scheduled executions

Example UI:

```text
Schedule

Mode:
○ Simple
● Cron

Expression:
*/30 * * * *

Timezone:
Europe/Istanbul

Next runs:
14:00
14:30
15:00
15:30
```

---

# 11. Concurrency Rules

Only one internet speed test should run at a time by default.

Possible state:

```text
Idle
Running
```

If a scheduled test fires while another test is running:

```text
skip the scheduled execution
```

This should be logged.

Future versions may support parallel tests for multiple interfaces/probes.

---

# 12. Dashboard

The dashboard should immediately answer:

> Is my internet connection currently healthy?

Primary cards:

```text
Status

Download
Upload
Latency
Jitter
Packet Loss
Last Test
```

Example:

```text
Internet Status
HEALTHY

Download
823 Mbps
-3.2% baseline

Upload
47 Mbps

Latency
8.3 ms

Jitter
1.4 ms

Packet Loss
0%
```

---

# 13. Dashboard Charts

Primary graph:

```text
Download / Upload Mbps
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

Additional charts:

- Latency
- Jitter
- Packet loss
- Download degradation percentage
- Upload degradation percentage

Optional combined chart:

```text
Latency + Jitter
```

---

# 14. History Page

A dedicated history page should show previous speed tests.

Columns:

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

- Pagination
- Date filtering
- Success/error filtering
- Engine filtering
- Sorting
- Manual test details view

---

# 15. Connection Health

Connection health should not be based on a single metric.

Possible states:

```text
Healthy
Degraded
Critical
Unknown
```

Example:

```text
Healthy
↓
Pending Degradation
↓
Degraded
↓
Recovering
↓
Healthy
```

This state machine helps prevent notification spam.

---

# 16. Static Alert Thresholds

Users should be able to configure rules such as:

```text
Download < 500 Mbps
Upload < 30 Mbps
Latency > 40 ms
Jitter > 15 ms
Packet Loss > 2%
```

Each metric should be optional.

Example rule:

```text
Name:
Slow Internet

Conditions:
Download < 500 Mbps
Latency > 40 ms

Trigger after:
3 consecutive failed tests

Recovery after:
2 consecutive healthy tests
```

---

# 17. Baseline Degradation Detection

Static thresholds alone are not enough.

Wanetra should calculate a connection baseline.

Initial algorithm:

```text
rolling median
```

Default period:

```text
7 days
```

Example:

```text
Baseline Download:
820 Mbps

Current:
510 Mbps

Degradation:
37.8%
```

Possible rule:

```text
Alert when download is
30% below baseline
for 3 consecutive tests.
```

Median should be preferred over average to reduce the impact of outlier measurements.

---

# 18. Future Baseline Improvements

Potential future algorithms:

- Time-of-day baseline
- Day-of-week baseline
- Percentile baseline
- EWMA
- Adaptive baseline
- Seasonal anomaly detection

These are not required for MVP.

---

# 19. Degradation Events

A degradation event should represent an incident rather than a single failed measurement.

Example:

```text
DegradationEvent

Id
StartedAt
EndedAt
Status
Reason
BaselineDownload
WorstDownload
BaselineUpload
WorstUpload
MaxLatency
MaxJitter
MaxPacketLoss
NotificationSent
```

Dashboard example:

```text
Internet degradation detected

Started:
18:42

Duration:
37 minutes

Worst Download:
412 Mbps

Baseline:
821 Mbps

Change:
-49.8%
```

---

# 20. Notification Architecture

Notification providers should use a common interface.

Example:

```csharp
public interface INotificationProvider
{
    string Name { get; }

    Task SendAsync(
        NotificationMessage message,
        CancellationToken cancellationToken);
}
```

Initial providers:

```text
NotificationProvider
├── ntfy
├── Gotify
├── Discord Webhook
├── Generic Webhook
└── SMTP
```

---

# 21. ntfy Support

Configuration:

```text
Server URL
Topic
Username
Password / Token
Priority
Tags
```

Example message:

```text
Internet degradation detected

Download:
823 → 412 Mbps (-49.9%)

Upload:
47 → 43 Mbps

Latency:
8 → 31 ms
```

Recovery:

```text
Internet connection recovered

Download:
817 Mbps

Latency:
9 ms

Incident duration:
42 minutes
```

---

# 22. Generic Webhook

Generic webhook support will make integrations flexible.

Configuration:

```text
URL
HTTP Method
Headers
Authentication
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

Future events:

```text
speedtest.completed
speedtest.failed
connection.degraded
connection.recovered
```

---

# 23. Prometheus Metrics

Endpoint:

```text
/metrics
```

Suggested metrics:

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

Labels should be used carefully to avoid high-cardinality metrics.

---

# 24. Health Endpoints

Application should provide:

```text
/health
/health/live
/health/ready
```

Possible checks:

- API alive
- SQLite accessible
- scheduler running

Do not run an actual speed test as part of application health checks.

---

# 25. Settings

Settings page sections:

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

# 26. General Settings

```text
Application Name
Timezone
Language
Theme
```

Initial language:

```text
English
```

Internationalization support can be added later.

---

# 27. Speed Test Settings

```text
Engine
Preferred server
Server ID
Interface
Timeout
Manual server selection
```

Future:

```text
Bind to network interface
Bind to source IP
```

This could be useful for multi-WAN environments.

---

# 28. Data Retention

Historical data should support automatic cleanup.

Example:

```text
Keep speed tests:
365 days

Keep logs:
30 days
```

Cleanup should run as a background maintenance task.

For MVP, SQLite should be enough for hundreds of thousands of measurements.

---

# 29. Database

Default:

```text
SQLite
```

Benefits:

- Single file
- No separate database container
- Backup is easy
- Suitable for homelab workloads

Database path:

```text
/data/wanetra.db
```

Use EF Core migrations.

Potential future database:

```text
PostgreSQL
```

PostgreSQL should not be required in v1.

---

# 30. API

Initial endpoints could follow this structure:

```text
/api/dashboard
/api/speedtests
/api/speedtests/latest
/api/speedtests/run

/api/schedules

/api/alerts
/api/alerts/events

/api/notifications/providers
/api/notifications/test

/api/settings

/api/system/info
```

---

# 31. Manual Speed Test

Endpoint:

```text
POST /api/speedtests/run
```

Response:

```json
{
  "status": "started"
}
```

The frontend should display:

```text
Speed test running...
```

and poll/query until the test completes.

Potential future improvement:

```text
Server-Sent Events
```

for real-time progress.

---

# 32. Logs

Use:

```text
Serilog
```

Destinations:

```text
Console
/data/logs/
```

Example events:

```text
Speed test started
Speed test completed
Speed test failed
Schedule executed
Schedule skipped
Degradation detected
Recovery detected
Notification sent
Notification failed
```

Sensitive notification credentials must never appear in logs.

---

# 33. Authentication

Authentication is not required for the first MVP if the application is expected to run behind:

- Traefik
- Nginx
- Caddy
- VPN
- Tailscale

However, built-in authentication should be considered for a later release.

Possible future options:

```text
Local account
OIDC
OAuth2
Forward Auth
```

---

# 34. Reverse Proxy Support

Application should work correctly behind:

- Traefik
- Nginx
- Caddy

Requirements:

- Forwarded headers support
- Configurable base URL if necessary
- HTTPS handled externally
- No mandatory TLS configuration inside Wanetra

---

# 35. Security

Minimum requirements:

- Validate cron expressions
- Validate webhook URLs
- Protect notification credentials
- Never expose credentials through API responses
- Do not log secrets
- Limit manual speed test abuse
- Prevent concurrent test execution
- Use request size limits where relevant
- Run container as non-root
- Read-only filesystem where feasible
- Minimal container image

---

# 36. Docker Image

Use a multi-stage Docker build.

Concept:

```text
Stage 1
Node
→ build React

Stage 2
.NET SDK
→ build backend

Stage 3
ASP.NET Runtime
→ final image
```

React output should be copied into:

```text
wwwroot/
```

Final image should contain only runtime dependencies.

---

# 37. Multi-Architecture Builds

GitHub Actions should build:

```text
linux/amd64
linux/arm64
```

Target devices:

```text
Standard x86 homelab server
Mini PC
Raspberry Pi 4
Raspberry Pi 5
ARM VPS
```

---

# 38. GitHub Repository

Repository:

```text
wanetra/
├── backend/
├── frontend/
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

# 39. GitHub Actions

Initial workflows:

## CI

On:

```text
pull_request
push
```

Tasks:

```text
dotnet restore
dotnet build
dotnet test

npm ci
npm run lint
npm run build
```

## Docker

On:

```text
tag
release
```

Build:

```text
amd64
arm64
```

Push:

```text
ghcr.io/USERNAME/wanetra
```

---

# 40. Versioning

Use semantic versioning:

```text
MAJOR.MINOR.PATCH
```

Example:

```text
0.1.0
0.2.0
1.0.0
```

During early development:

```text
0.x
```

---

# 41. MVP Scope

Version:

```text
v0.1
```

Must include:

- React dashboard
- ASP.NET Core API
- SQLite
- LibreSpeed engine
- Manual speed test
- Scheduled tests
- Cron scheduling
- Historical results
- Download/upload charts
- Latency chart
- Jitter support
- Packet loss support where available
- Static alert thresholds
- Rolling median baseline
- Degradation detection
- Degradation events
- ntfy notifications
- Generic webhook
- Prometheus metrics
- Docker image
- amd64 support
- arm64 support

---

# 42. v0.2 Ideas

Potential additions:

- Gotify
- Discord
- SMTP
- Ookla engine
- Data retention settings
- CSV export
- JSON export
- Dashboard customization
- ISP information
- External IP tracking
- Server comparison
- More detailed incident timeline

---

# 43. v0.3 Ideas

Potential additions:

- Built-in authentication
- OIDC
- Multiple WAN interfaces
- Multiple schedules
- Per-interface monitoring
- PostgreSQL support
- Additional alert rules

---

# 44. Future: Multi-Probe Architecture

A later major feature could introduce remote probes.

Architecture:

```text
                    Wanetra Server
                           │
             ┌─────────────┼─────────────┐
             │             │             │
             ▼             ▼             ▼
          Home Pi       Office Pi      VPS
          Agent          Agent         Agent
```

A lightweight agent:

```text
wanetra-agent
```

Responsibilities:

- Run speed tests
- Run latency measurements
- Run packet loss measurements
- Send measurements to server

Possible API:

```text
POST /api/agents/{id}/measurements
```

Agents would authenticate using API keys.

---

# 45. Future: Continuous Connectivity Monitoring

Speed tests should not eventually be the only source of health data.

Future lightweight monitoring:

```text
Ping every 5 seconds
DNS lookup every 30 seconds
HTTP probe every 30 seconds
Speed test every 30 minutes
```

This would allow Wanetra to detect outages without continuously consuming large amounts of bandwidth.

Potential metrics:

```text
packet loss
DNS latency
HTTP latency
TCP connectivity
gateway reachability
internet reachability
```

---

# 46. Future: Outage Detection

Possible distinction:

```text
LAN issue
Gateway issue
DNS issue
Internet outage
Performance degradation
```

Example:

```text
Gateway reachable
1.1.1.1 unreachable
DNS unreachable

→ probable WAN outage
```

This should not be part of the first MVP.

---

# 47. Project Identity

Wanetra should be positioned as:

> A self-hosted internet and WAN health monitoring platform for homelabs.

Not simply:

> A speedtest history dashboard.

Core differentiation:

```text
Speed Tests
+
Connection Health
+
Degradation Detection
+
Alerts
+
Prometheus
+
Self-hosted integrations
```

---

# 48. Initial Development Order

Recommended implementation order:

```text
1. Repository structure
2. ASP.NET Core base project
3. React base project
4. SQLite + EF Core
5. SpeedTestResult model
6. Speed test engine abstraction
7. LibreSpeed integration
8. Manual speed test
9. History API
10. Dashboard
11. Scheduler
12. Cron configuration
13. Baseline calculations
14. Alert engine
15. Degradation events
16. ntfy
17. Generic webhook
18. Prometheus metrics
19. Docker image
20. Multi-arch GitHub Actions
21. Documentation
22. First release
```

---

# 49. Decisions Still Open

These items should be reviewed before development begins:

- LibreSpeed execution strategy
  - embedded binary
  - separate process
  - internal implementation
- Whether packet loss measurement should use the speed test engine or a separate probe
- Initial authentication strategy
- Whether multiple schedules are required in v1
- Default baseline duration
- Default degradation thresholds
- Exact retention defaults
- Whether notifications belong to individual alert rules or global settings
- Whether SMTP belongs in v1 or later
- Whether Ookla support belongs in v1 or later
- License choice

---

# 50. Suggested MVP Defaults

Proposed initial defaults:

```text
Speed Test Interval:
30 minutes

Baseline:
7 days rolling median

Degradation:
30% below baseline

Required failures:
3 tests

Recovery:
2 healthy tests

History retention:
365 days

Timezone:
System timezone

Database:
SQLite

Notifications:
Disabled by default

Prometheus:
Enabled
```

---

# 51. Non-Goals for v1

Avoid implementing these too early:

- Kubernetes operator
- Distributed database
- Message broker
- Redis
- Microservices
- Complex CQRS
- Event sourcing
- Machine learning
- Multi-user permissions
- Enterprise RBAC
- Mobile application
- Native desktop application

The initial priority should be a reliable, easily deployable homelab application.

---

# 52. Definition of Done for v0.1

A user should be able to:

1. Start Wanetra using Docker Compose.
2. Open the web dashboard.
3. Run a manual speed test.
4. Configure a cron schedule.
5. Automatically collect connection metrics.
6. View historical results.
7. View download/upload/latency trends.
8. Configure a degradation threshold.
9. Receive an ntfy notification when degradation occurs.
10. Receive a recovery notification.
11. Scrape Wanetra with Prometheus.
12. Run the same Docker image on both amd64 and arm64 systems.
