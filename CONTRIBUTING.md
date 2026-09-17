# Contributing to Wanetra

Use focused branches and pull requests. Do not commit feature work directly to `main`.

## Requirements

- .NET 10 SDK
- Bun

## Local checks

```fish
dotnet restore backend/Wanetra.slnx
dotnet build backend/Wanetra.slnx
dotnet test backend/Wanetra.slnx
dotnet format backend/Wanetra.slnx --verify-no-changes

cd frontend
bun install --frozen-lockfile
bun run lint
bun run build
```

## Database migrations

```fish
dotnet ef migrations add <Name> --project backend/Wanetra.Infrastructure --startup-project backend/Wanetra.Api --output-dir Persistence/Migrations
dotnet format backend/Wanetra.slnx
```

Run `dotnet format` afterwards because generated migrations are written with a
byte order mark, which the CI format check rejects.

## Pull requests

Keep changes focused, include relevant tests, and update documentation when behavior or configuration changes. Use concise conventional commit messages such as `feat:`, `fix:`, `docs:`, `test:`, `build:`, or `ci:`.
