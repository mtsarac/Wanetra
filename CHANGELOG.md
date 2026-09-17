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

### Fixed

- Container health check: the runtime image ships no `wget`, so the container
  never reported healthy.
