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

### Fixed

- Speed test status: a failed run now reports the trigger and start time of the
  run that failed instead of leaving both empty.
- Container health check: the runtime image ships no `wget`, so the container
  never reported healthy.
