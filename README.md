# Wanetra

A self-hosted internet and WAN health monitoring application for homelabs.

## Status

Early development / pre-release.

## What is Wanetra?

Wanetra will collect WAN speed and connection-health measurements, retain history, detect degradation, send notifications, and expose Prometheus metrics. It is intended to run as one Docker container with SQLite storage.

## Available

- ASP.NET Core health endpoints: `/health`, `/health/live`, `/health/ready`
- SQLite storage with EF Core migrations applied at startup
- Speed test API: manual runs, execution status, latest result, filtered history
- Scheduled speed tests: cron expression plus timezone, run by a background worker
- React application shell
- Container build foundation

## Planned capabilities

- LibreSpeed-compatible test engines
- Historical metrics and dashboard charts
- Baseline-based degradation detection and alerts
- ntfy and generic webhook notifications
- Prometheus metrics

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

A run outlives the HTTP request that starts it, so poll `/api/speedtests/status`
instead of waiting on the response.

The schedule ships disabled with `*/30 * * * *` in `Europe/Istanbul`.
Saving wakes the worker right away, so there's no need to restart.
All `nextRuns` timestamps are UTC. A run whose time has already passed is
skipped, it's never caught up.

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

Build and run the bootstrap image:

```fish
docker build -t wanetra:dev .
docker run --rm -p 8080:8080 -v (pwd)/data:/data wanetra:dev
```

The database is created at `$WANETRA_DATA_PATH/wanetra.db` (`/data/wanetra.db` in the
container). Mount that directory to keep measurement history between restarts.

## Project documentation

- [Project plan](docs/wanetra-project-plan-human.md)
- [Implementation plan](docs/wanetra-coding-agent-plan.md)

## License

License selection remains open.
