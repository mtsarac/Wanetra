# Phase 5 Scheduler Design

**Date:** 2026-09-18
**Status:** Approved

## Scope

Add one persistent speed-test schedule using a five-field cron expression and IANA timezone. Expose configuration and upcoming UTC runs through API. Trigger existing `SpeedTestCoordinator`, which remains only concurrency boundary.

Out of scope: multiple schedules, generic scheduler framework, queue, retries, catch-up/backfill, dashboard UI, alerts, notifications.

## Persistence and Defaults

Store one deterministic SQLite row with `Id = 1`:

- `Enabled = false`
- `CronExpression = "*/30 * * * *"`
- `Timezone = "Europe/Istanbul"`
- `UpdatedAt` in UTC

Repository always reads and upserts key `1`; it never relies on `First()` ordering.

## Scheduling

A hosted worker loads settings, calculates next occurrence with Cronos and configured `TimeZoneInfo`, and waits with application cancellation plus an update signal. A successful schedule update signals worker immediately; worker abandons old delay, reloads row `1`, and calculates a new occurrence.

At a due occurrence worker calls `SpeedTestCoordinator.TryStart(SpeedTestTrigger.Scheduled, stoppingToken)` once. Busy coordinator means skip and log; no queue or retry.

Every calculation starts from current UTC time. Runs missed while application was stopped or delayed are not replayed. Restart computes only next future occurrence: explicit no catch-up/no backfill.

Invalid persisted configuration logs warning and waits for update or short retry delay without terminating host.

## API

- `GET /api/schedule`: returns `enabled`, `cronExpression`, `timezone`, `updatedAt`, and five upcoming UTC `nextRuns` (empty when disabled).
- `PUT /api/schedule`: atomically validates and saves `enabled`, `cronExpression`, and `timezone`; wakes worker after commit.
- `GET /api/schedule/next-runs?count=5`: returns bounded upcoming UTC occurrences; empty when disabled.

Invalid cron, timezone, or count returns HTTP 400 using existing `{ "code", "message" }` contract.

## Tests

- deterministic row `Id = 1`, defaults, save/reload;
- valid/invalid five-field cron and timezone;
- real DST transition using `Europe/Berlin` or `America/New_York` while production default remains `Europe/Istanbul`;
- disabled schedule has no occurrences and does not execute;
- update wakes worker and replaces old delay immediately;
- due schedule starts one scheduled test;
- busy coordinator skips execution;
- restart/missed occurrences produce no catch-up/backfill;
- invalid persisted settings do not terminate host;
- API response, update, validation, and next-run behavior.

## Acceptance Criteria

- Schedule persists across restarts in row `Id = 1`.
- API updates wake worker immediately.
- Enabled schedule executes future occurrences automatically.
- Disabled schedule does not execute.
- Missed occurrences are never replayed.
- Existing coordinator prevents manual/scheduled overlap.
- Focused and full validation pass.
