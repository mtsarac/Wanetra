# Phase 5 Scheduler Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add one persistent, timezone-aware cron scheduler that triggers existing speed-test coordinator and exposes schedule configuration plus upcoming runs through API.

**Architecture:** SQLite stores exactly one schedule row with `Id = 1`. Cronos calculates future UTC occurrences from current UTC time. A hosted worker waits until next occurrence but schedule updates wake it immediately so changed cron/timezone applies without waiting for old delay.

**Tech Stack:** .NET 10, ASP.NET Core Minimal API, EF Core SQLite, Cronos, BackgroundService, xUnit integration tests.

## Global Constraints

- One schedule only; no generic scheduler framework, queue, retry mechanism, or multiple schedules.
- Defaults: `Enabled = false`, `CronExpression = "*/30 * * * *"`, `Timezone = "Europe/Istanbul"`.
- Store schedule data in SQLite row `Id = 1`; never rely on `First()` semantics.
- Use UTC timestamps in API responses and persistence.
- Schedule updates must wake worker immediately.
- No catch-up/no backfill: missed occurrences during downtime are never replayed.
- Existing `SpeedTestCoordinator` is only concurrency boundary.
- Busy coordinator means skipped scheduled execution, with logging and no retry queue.
- Invalid persisted configuration must not terminate host.
- DST-specific tests use `Europe/Berlin` or `America/New_York`; production default remains `Europe/Istanbul`.
- Preserve existing `{ "code", "message" }` API errors.

## Tasks

### Task 1: Persistence

Files: `ScheduleSettings.cs`, `IScheduleSettingsRepository.cs`, `ScheduleSettingsRepository.cs`, DI, persistence tests.

Steps:
- write failing tests for defaults, deterministic `Id = 1`, save/reload, single-row update;
- implement domain defaults and repository upsert by key `1`;
- register repository;
- run `dotnet test backend/Wanetra.slnx --filter FullyQualifiedName~SchedulePersistenceTests`.

### Task 2: Calculator and Service

Files: `ScheduleCalculator.cs`, `ScheduleService.cs`, app csproj/DI, calculator tests.

Steps:
- write failing tests for valid/invalid cron, timezone, bounded count, disabled no runs, `Europe/Berlin` DST, no backfill by calculating only future runs from now;
- add Cronos if absent;
- implement validation and next-run calculation from UTC;
- implement service update with validation, UTC `UpdatedAt`, persistence, and wake signal;
- run focused calculator tests.

### Task 3: Wakeable Worker

Files: `ScheduleWorker.cs`, `ScheduleChangeSignal.cs`, worker tests.

Steps:
- write failing tests for disabled no execution, due execution, busy coordinator skip, update wake, invalid persisted settings survival, cancellation exit, no catch-up after old due time;
- implement worker using update signal plus shutdown token;
- always recompute next occurrence from current UTC after wake/run;
- run focused worker tests.

### Task 4: API

Files: `ScheduleContracts.cs`, `ScheduleEndpoints.cs`, `Program.cs`, API tests.

Steps:
- write failing tests for GET defaults, PUT persistence+wake, next-runs endpoint, invalid cron/timezone/count errors;
- implement DTOs and endpoints using existing `ApiError` style;
- map endpoints;
- run focused API tests.

### Task 5: Docs and Verification

Files: README, CHANGELOG, migrations only if model snapshot requires change.

Steps:
- inspect EF snapshot; add migration only if model changed;
- document schedule API and defaults;
- run full backend tests/build;
- run frontend lint/build with Bun;
- run LSP diagnostics and `git diff --check`.

### Task 6: Git and PR

Steps:
- split commits by concern with conventional commit messages;
- push `feat/scheduler`;
- open PR against `main`;
- leave PR open for review.
