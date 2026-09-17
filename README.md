# Wanetra

A self-hosted internet and WAN health monitoring application for homelabs.

## Status

Early development / pre-release.

## What is Wanetra?

Wanetra will collect WAN speed and connection-health measurements, retain history, detect degradation, send notifications, and expose Prometheus metrics. It is intended to run as one Docker container with SQLite storage.

## Available

- ASP.NET Core health endpoints: `/health`, `/health/live`, `/health/ready`
- React application shell
- Container build foundation

## Planned capabilities

- Manual and scheduled LibreSpeed-compatible tests
- Historical metrics and dashboard charts
- Baseline-based degradation detection and alerts
- ntfy and generic webhook notifications
- Prometheus metrics

## Tech stack

- Backend: .NET 10, ASP.NET Core Minimal API
- Frontend: React, TypeScript, Vite, Tailwind CSS, shadcn/ui foundation
- Storage: SQLite (planned)

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

## Project documentation

- [Project plan](docs/wanetra-project-plan-human.md)
- [Implementation plan](docs/wanetra-coding-agent-plan.md)

## License

License selection remains open.
