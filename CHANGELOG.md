# Changelog

All notable changes to Wanetra will be documented here.

## Unreleased

### Added

- Initial repository bootstrap.
- SQLite persistence with EF Core: speed test results, schedule settings, alert
  rules, degradation events, and notification configurations.
- Database migrations applied at startup and a database readiness health check.
- Speed test engine abstraction with a LibreSpeed implementation that wraps
  `librespeed-cli`, and a coordinator that allows only one test at a time.
- Speed test API: start a manual run, poll its status, and read the latest
  result, a single result, or paged history filtered by date range, outcome,
  and engine.
- Scheduled speed tests: `GET`/`PUT /api/schedule` plus
  `GET /api/schedule/next-runs`, backed by a background worker that wakes
  immediately when settings are saved. Defaults to disabled with
  `*/30 * * * *` in `Europe/Istanbul`; upcoming runs are reported in UTC,
  missed runs are skipped instead of caught up, and a bad cron expression
  or timezone answers `400`.

### Fixed

- Speed test status: a failed run now reports the trigger and start time of the
  run that failed instead of leaving both empty.
- Container health check: the runtime image ships no `wget`, so the container
  never reported healthy.
