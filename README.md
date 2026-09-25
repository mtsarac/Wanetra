# Wanetra

A self-hosted internet and WAN health monitoring application for homelabs.

## Status

Early development / pre-release.

## What is Wanetra?

Wanetra will collect WAN speed and connection-health measurements, retain history, detect degradation, send notifications, and expose Prometheus metrics. It is intended to run as one Docker container with SQLite storage.

## Available

- ASP.NET Core health endpoints: `/health`, `/health/live`, `/health/ready`
- SQLite storage with EF Core migrations applied at startup
- LibreSpeed speed tests with single-run concurrency and paged history
- Cron scheduling with timezone-aware next-run previews
- React dashboard with current metrics, history charts, and rolling baselines
- Persistent degradation detection with ntfy and generic webhook notifications
- Prometheus metrics at `/metrics`
- Configurable daily speed-test retention (365 days by default)
- Single-container Docker image with persistent `/data`

## Configuration

| Variable | Default | Purpose |
| --- | --- | --- |
| `WANETRA_DATA_PATH` | `/data` | Database and application data directory |
| `WANETRA_PORT` | `8080` | Host port in the example Compose file |
| `DataRetention__Days` | `365` | Days of speed-test history to retain |
| `TZ` | `Europe/Istanbul` | Container timezone |

Retention cleanup runs at startup and then every 24 hours. Results exactly on
the cutoff remain; only older results are deleted.

## API

| Method | Path | Purpose |
| --- | --- | --- |
| `POST` | `/api/speedtests/run` | Start a manual test. Answers `202` right away, or `409` when a test is already running. |
| `GET` | `/api/speedtests/status` | Execution state: `idle`, `running`, or `failed`. |
| `GET` | `/api/speedtests/latest` | Most recent result. |
| `GET` | `/api/speedtests/{id}` | A single result. |
| `GET` | `/api/speedtests` | Paged history. |
| `GET` | `/api/schedule` | Current schedule plus the next 5 runs. |
| `PUT` | `/api/schedule` | Save `{enabled, cronExpression, timezone}`. Bad cron or timezone answers `400`. |
| `GET` | `/api/schedule/next-runs?count=` | Upcoming runs in UTC (`count` 1 to 100, default 5). Empty when the schedule is off. |
| `GET` | `/api/baseline` | Seven-day rolling download and upload baselines. |
| `GET` | `/api/alerts/active` | Current persisted degradation event (`404` when none is open). |
| `GET` | `/api/alerts/events?count=` | Recent degradation events (1–100, default 20). |
| `GET`/`PUT` | `/api/alerts/rule` | Read or save the single alert rule and transition counts. |
| `GET`/`PUT` | `/api/notifications` | Read or save ntfy and generic webhook configuration. |
| `POST` | `/api/notifications/test` | Send a test notification to a saved or draft provider. |
| `GET` | `/api/settings/retention` | Read the configured measurement retention period. |
| `GET` | `/metrics` | Prometheus scrape endpoint. |

A run outlives the HTTP request that starts it, so poll `/api/speedtests/status`
instead of waiting on the response.

The schedule ships disabled with `*/30 * * * *` in `Europe/Istanbul`.
Saving wakes the worker right away, so there's no need to restart.
All `nextRuns` timestamps are UTC. A run whose time has already passed is
skipped, it's never caught up.

Fresh installs start with an enabled 30% download-baseline alert. The baseline
needs ten valid samples; three consecutive unhealthy successful tests open an
incident, and two recovery tests close it. Configure notifications separately.

History query parameters: `from` and `to` (ISO-8601, UTC), `success`, `engine`,
`sort` (`asc` or `desc`, default `desc`), `page` (default 1), and `pageSize`
(default 50, maximum 200).

Errors carry `{"code": "...", "message": "..."}`.

## Tech stack

- Backend: .NET 10, ASP.NET Core Minimal API
- Frontend: React, TypeScript, Vite, Tailwind CSS, shadcn/ui foundation
- Storage: SQLite, Entity Framework Core

## Development

Requirements: .NET 10 SDK and Bun.

```fish
dotnet restore backend/Wanetra.slnx
dotnet build backend/Wanetra.slnx
dotnet test backend/Wanetra.slnx

cd frontend
bun install --frozen-lockfile
bun run lint
bun run build
bun run dev
```

## Docker

Build and run the application locally with Docker Compose:

```sh
cp compose.example.yml compose.yml
docker compose up -d --build
```

Compose builds the image for the local Docker builder's platform; the Dockerfile
selects the matching LibreSpeed binary for that architecture. No prebuilt Wanetra
image is required.

The database lives in the Docker-managed `wanetra-data` volume mounted at
`$WANETRA_DATA_PATH` (`/data` by default). Docker initializes the volume with
the application's non-root ownership. Set `DataRetention__Days` in `.env` to
change the 365-day default.

## Project documentation

- [Project plan](docs/wanetra-project-plan-human.md)
- [Implementation plan](docs/wanetra-coding-agent-plan.md)

## License

License selection remains open.
