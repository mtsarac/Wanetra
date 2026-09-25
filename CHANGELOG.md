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
- Degradation and recovery notifications via ntfy and generic webhook:
  `GET`/`PUT /api/notifications` (secrets redacted, blank fields keep stored
  values) plus `POST /api/notifications/test`, dispatched once per
  degradation-event transition; provider failures are logged without crashing
  the run.
- Prometheus `/metrics` endpoint with low-cardinality latest speed-test gauges,
  completion/failure counters, HTTP metrics, and connection degradation state.
- Configurable speed-test retention (default 365 days) with a daily background
  cleanup that deletes only expired measurements.
- Alert rule API: `GET`/`PUT /api/alerts/rule` stores the single editable rule
  and validates thresholds and transition counts.
- Production image includes checksum-verified LibreSpeed CLI 1.0.14 binaries
  for amd64 and arm64, preserving the upstream license text.
- Docker Compose uses a managed `/data` volume so a first run keeps the
  application non-root without host bind-mount ownership setup.
- Docker Compose now builds Wanetra locally for the Docker builder's platform;
  the Dockerfile selects the matching LibreSpeed CLI instead of requiring a
  prebuilt Wanetra image.
- React Router pages for dashboard, filtered/paginated test history with detail
  rows, alert-rule editing, notifications, and schedule/retention settings.
- Dashboard includes speed and connection-quality charts with 6-hour through
  90-day and custom time ranges.
- Tag-triggered GHCR release workflow publishes amd64 and arm64 container
  manifests.
- Prometheus latest-result and degradation gauges restore from SQLite on startup.
- Read-only alert event history at `GET /api/alerts/events?count=` and an
  incident list alongside threshold configuration.
- Fresh databases seed the documented 30% download-baseline degradation rule,
  so alert reads and evaluations work before the user edits settings.
- A notification retry worker tracks opening and recovery delivery separately
  for each event destination; schema migration backfills legacy event-level
  notification flags into per-destination delivery records.
- Frontend regression tests cover paginated history, settings, notification
  secret preservation, dashboard run state, and API errors.

### Changed

- Failed speed tests now expose a failure kind. Only network failures and
  unusable measurements affect degradation state; local CLI/process failures and
  caller cancellation do not count as WAN degradation.
- Disabling an alert rule closes an open incident with status `disabled` and
  clears the degradation gauge. Re-enabling starts with no open incident.
- Notification delivery is tracked per enabled configuration snapshot. Successful
  channels are not retried when another channel fails; recovery waits for that
  channel's opening delivery. Destination IDs remain stable across edits.
- Notification reads return only safe provider metadata, never URLs or secret
  values. Successful empty HTTP responses are accepted, and chart history pages
  are fetched sequentially.
- Dashboard charts load all pages in the selected range, independently from the
  six most recent readings.
- LibreSpeed's unavailable packet-loss measurement is exposed as unavailable
  instead of a zero-valued Prometheus gauge; its alert threshold is disabled in
  the UI.
- Notification destination URLs are restricted to valid HTTP(S) URLs, while
  private addresses remain available for self-hosted providers.
- Example Compose port publishing now binds to loopback unless explicitly
  configured otherwise. Wanetra has no built-in authentication; see README
  security guidance before allowing network access.


### Fixed

- Speed test status: a failed run now reports the trigger and start time of the
  run that failed instead of leaving both empty.
- Container health check: the runtime image ships no `wget`, so the container
  never reported healthy.
