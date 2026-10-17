# Wanetra agent guide

Self-hosted internet speed monitor, pre-1.0. `backend/` is .NET (`backend/Wanetra.slnx`). `frontend/` is React + Vite, managed with Bun.

## Commands

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

Migrations: `dotnet ef migrations add <Name> --project backend/Wanetra.Infrastructure --startup-project backend/Wanetra.Api --output-dir Persistence/Migrations`, then run `dotnet format backend/Wanetra.slnx`. Generated migrations get a BOM that the CI format check rejects.

## Rules

- Work on focused branches and open PRs. Never commit feature work directly to `main`.
- Conventional commit messages: `feat:`, `fix:`, `docs:`, `test:`, `build:`, `ci:`.
- No AI attribution in commits, PRs, or docs.
- Never commit secret files: `.env*` (except `.env.example`), credentials, keys, or tokens. Ask the user before committing any file that may contain secrets.
- Read or use secret files only with the user's explicit approval for the task.
- Update docs and configuration references when behavior or config changes.

## Reference

- Contributing and PR details: `CONTRIBUTING.md`
- Releases: `docs/RELEASING.md`
- Security policy: `SECURITY.md`
