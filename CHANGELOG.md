# Changelog

All notable changes to Wanetra will be documented here. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

## [0.4.1] - 2026-10-19

### Fixed

- Ping probes always fell back to TCP checks in the Docker image, because .NET pings through the system `ping` utility when it has no raw-socket privilege and the image did not contain one. The image now includes `iputils-ping`.

## [0.4.0] - 2026-10-19

### Added

- Local network scanning, off by default. Finds the devices on the LAN through the kernel's ARP table (no extra privileges), names them with mDNS, NetBIOS, reverse DNS and UPnP, and looks up the manufacturer in the IEEE registry built into the image. The first scan learns the network silently; later unknown devices raise a notification, watched devices raise one when they go offline and when they return, and a watched device gets a ping target that follows its address. Only private ranges of at most 1,022 addresses are scanned.
- **Devices** page with the inventory, filters, per-device history, naming, "known" and "watch" flags, and a scan button; Settings → Network scanning; `/api/lan/*` and `/api/devices/*` endpoints; `wanetra_lan_*` metrics; `Lan__*` environment variables.
- `compose.lan.yaml`, which runs Wanetra with host networking (Linux only) so it can see the LAN, binds the UI to `127.0.0.1` by default and fixes the container health check for the host port.
- `scripts/update-oui.ts` regenerates the manufacturer table.
- Connection monitoring. Wanetra probes the default gateway, three public IPs and a DNS lookup every 10 seconds (ping with a TCP fallback, TCP, DNS and HTTP checks), keeps raw samples with per-minute and per-hour summaries, and opens an outage when every internet target stays unreachable. The outage says whether the local network, the internet link or DNS is at fault. Targets can be added, paused, edited and deleted.
- Outage notifications through the existing ntfy and webhook destinations. A notice that cannot be sent while offline is delivered after the connection returns.
- **Connectivity** page and a live connection summary on the Dashboard, with uptime for 24 hours, 7 days and 30 days, a latency and packet-loss chart and an outage timeline.
- `/api/probes/*`, `/api/outages` and `/api/uptime` endpoints, probe and outage Prometheus metrics, and Settings → Connection monitoring (`Probes__*` environment variables).
- Sign-in with a single account. On first start Wanetra prints a one-time setup code in its log; the web UI uses it to create the account. Authentication can be turned off and on again under Settings → Security, and `WANETRA_AUTH=disabled` (or `enabled`) overrides it from the environment, which is also the way back in after a lost password.
- Change-password form, session cookie (`HttpOnly`, `SameSite=Strict`, 30 days), throttling of repeated failed sign-ins, and a CSRF header check for state-changing requests.
- `/metrics` bearer token, generated under Settings → Security.
- `/api/auth/*` endpoints.

### Changed

- Scheduled speed tests are skipped while an outage is open.
- Probe results gathered during a speed test are stored but do not count towards an outage.
- The Compose files set `net.ipv4.ping_group_range` so the non-root container can ping.
- **Breaking:** with authentication on (the default for a new or upgraded install), every `/api` route, `/openapi/v1.json` and `/metrics` need a session, and an upgraded install asks for account setup on first visit. Prometheus gets `401` until you add the metrics token. Set `WANETRA_AUTH=disabled` to keep the previous open behavior. `/health*` stays open.

## [0.3.1] - 2026-10-18

### Added

- Wanetra is licensed under AGPL-3.0; the `LICENSE` file is now in the repository.
- OpenAPI 3 description of the API at `GET /openapi/v1.json`.

### Changed

- Renamed `docker-compose.yaml` to `compose.yaml` and `compose.example.yml` to `compose.example.yaml`. Use `docker compose -f compose.example.yaml up -d --build` to build from source.
- `/metrics` always answers in the Prometheus text format; OpenMetrics content negotiation is no longer offered. The exposed metric names are unchanged.

### Fixed

- Wanetra gauges live in a per-instance Prometheus registry instead of static fields, which removes a flaky test that read gauges another test host had overwritten.

## [0.3.0] - 2026-10-08

Includes everything since the initial repository bootstrap; earlier pre-release tags and releases were removed.

### Added

- Runtime settings system with `GET`/`PUT /api/settings`: change speed-test engine,
  timeout, server ID, and retention days via UI or API without container restart.
  Supports precedence env > stored > default; non-empty env variables lock fields in the UI.
  Restricted executable paths remain configurable only via environment variables for security.
  Ookla license acceptance can be given and stored via the UI. Replaces `GET /api/settings/retention`.

- Cloudflare and Ookla speed-test engines alongside LibreSpeed, selected with
  `SpeedTest__Engine`. Cloudflare wraps checksum-verified `cfspeedtest` 2.2.2,
  bundled for amd64 and arm64. Ookla uses the official Speedtest CLI, which is
  not bundled because its EULA forbids redistribution: with
  `SpeedTest__Ookla__AcceptLicense=true` Wanetra downloads the sha256-pinned
  official binary from Ookla into the data directory on first use, and falls
  back to your own binary at `SpeedTest__Ookla__ExecutablePath` if that download
  fails. Ookla also reports packet loss.
- `docs/RELEASING.md` for the maintainer release process, moved out of the README.
- Added a persistent 21-theme light/dark palette selector and self-hosted JetBrains Mono typography.
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
- The local-build Compose example selects the Docker builder's platform; the
  Dockerfile installs the matching LibreSpeed CLI.
- React Router pages for dashboard, filtered/paginated test history with detail
  rows, alert-rule editing, notifications, and schedule/retention settings.
- Dashboard includes speed and connection-quality charts with 6-hour through
  90-day and custom time ranges.
- Tag-triggered GHCR release workflow publishes amd64 and arm64 container
  manifests.
- Root `docker-compose.yaml` pulls a pinned GHCR release and starts with a
  Docker-managed data volume without additional configuration.
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

- Refreshed the monitoring interface with a responsive signal-focused dashboard,
  updated chart colors, consistent form surfaces, keyboard navigation, and a
  matching application icon.
- Reworked the frontend with Tailwind CSS and local shadcn/ui primitives: warm
  monochrome surfaces, system monospace typography, muted semantic colors,
  responsive bento layouts, and reduced-motion-aware reveal transitions.

- GHCR now publishes `latest` and a commit-SHA image after successful `main`
  CI; version tags publish semantic-version images without overwriting `latest`.
  The default Compose deployment tracks `latest` instead of `0.1.0`.

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
- CI runs for version tags no longer rebuild containers; releases promote the tested `main` commit image and verify its amd64/arm64 manifest.
- Dependency review now blocks high-severity dependency changes, CodeQL uploads results, and Dependabot tracks the Bun lockfile.
- GitHub Releases are marked Latest when their tag is the highest SemVer release on `main`; backports to older lines are not.

[Unreleased]: https://github.com/mtsarac/Wanetra/compare/v0.4.1...HEAD
[0.4.1]: https://github.com/mtsarac/Wanetra/compare/v0.4.0...v0.4.1
[0.4.0]: https://github.com/mtsarac/Wanetra/compare/v0.3.1...v0.4.0
[0.3.1]: https://github.com/mtsarac/Wanetra/compare/v0.3.0...v0.3.1
[0.3.0]: https://github.com/mtsarac/Wanetra/releases/tag/v0.3.0
