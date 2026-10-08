# Wanetra

Wanetra is a self-hosted monitor for your internet connection. It runs speed tests on a schedule, keeps the history in SQLite, shows it in a web dashboard, and tells you through ntfy or a webhook when your connection degrades and when it recovers. It also exposes Prometheus metrics. It runs as a single Docker container.

It is pre-1.0. The API and the database schema can still change between releases, so back up the data volume before upgrading.

## Features

- Speed tests with one of three engines: LibreSpeed, Cloudflare, or Ookla
- Manual runs and cron schedules with a timezone, one test at a time
- History with filtering and paging, plus a seven-day rolling baseline
- Degradation alerts (thresholds and drops against the baseline) with recovery notices, sent through ntfy or a generic webhook
- Prometheus metrics at `/metrics`
- Automatic cleanup of old results (365 days by default)

## Quick start

You need Docker with Compose.

```sh
git clone https://github.com/mtsarac/Wanetra.git
cd Wanetra
docker compose up -d
```

The dashboard is at `http://127.0.0.1:8080` on the Docker host. The scheduler is off by default, so press **Run speed test** or enable a schedule under Settings.

The Compose file pulls `ghcr.io/mtsarac/wanetra:latest` (linux/amd64 and linux/arm64) and keeps data in a Docker-managed volume named `wanetra-data`, mounted at `/data`. No `.env` file or host directory is needed.

To build the image from source instead, run `docker compose -f compose.example.yml up -d --build`.

### Updating and pinning versions

Publishing a new image does not restart a running container. To update, run:

```sh
git pull --ff-only
docker compose pull
docker compose up -d
```

`latest` follows `main`. That is convenient for a homelab but it is not a fixed version. To pin a release or roll back, set `WANETRA_VERSION` to a published `X.Y.Z` or to `sha-<40-character commit SHA>` before pulling. Back up the volume first: rolling back the image does not undo database migrations.

## Configuration

Most settings can be changed directly from the Settings page in the web UI. They take effect on the next test run or retention cleanup without restarting the container.

Setting values follow this precedence order:

1. Non-empty environment variable (highest priority).
2. Stored value in the database (set via the UI or API).
3. Built-in default in code.

An empty or whitespace environment variable counts as unset. This ensures Docker Compose variable fallbacks such as `${SpeedTest__Engine:-}` do not override stored values.

When a setting is set via an environment variable, it becomes locked and cannot be edited in the UI or changed via the API. The UI displays the environment variable name and shows the setting as read-only.

Executable paths are restricted (`speedtest.librespeed.executablePath`, `speedtest.cloudflare.executablePath`, `speedtest.ookla.executablePath`). Because Wanetra does not yet require authentication, editable binary paths would allow arbitrary command execution by anyone with access to the UI. Restricted settings cannot be changed through the UI or API; they can only be set via environment variables.

Common configuration variables:

| Variable | Default | Purpose |
| --- | --- | --- |
| `WANETRA_DATA_PATH` | `/data` | Directory for the database and downloaded tools |
| `WANETRA_PORT` | `8080` | Host port published by the Compose files |
| `WANETRA_BIND_ADDRESS` | `127.0.0.1` | Host interface published by the Compose files |
| `WANETRA_VERSION` | `latest` | Image tag used by `docker-compose.yaml` |
| `DataRetention__Days` | `365` | Days of speed-test history to keep |
| `SpeedTest__Engine` | `librespeed` | Active engine: `librespeed`, `cloudflare`, or `ookla` |
| `SpeedTest__Ookla__AcceptLicense` | `false` | Required for `ookla`. Read [Ookla and its license](#ookla-and-its-license) first |
| `SpeedTest__Ookla__ExecutablePath` | empty | Optional fallback binary for `ookla` |
| `TZ` | `Europe/Istanbul` | Container timezone |

Cleanup of old results runs at startup and then every 24 hours. A result exactly at the cutoff is kept; only older ones are deleted.

Every engine setting can also be configured via its environment variable (replacing `.` with `__` and using configuration keys). Any non-empty value locks that setting in the UI.

## Speed-test engines

One engine runs at a time. You can select the active engine in the Settings page or lock it with `SpeedTest__Engine`. The change applies immediately to the next test run. Each result stores the engine that produced it, and `GET /api/speedtests?engine=` filters on it.

| Engine | Tool | Bundled in the image | Packet loss |
| --- | --- | --- | --- |
| `librespeed` | [librespeed-cli](https://github.com/librespeed/speedtest-cli) | yes | no |
| `cloudflare` | [cfspeedtest](https://github.com/code-inflation/cfspeedtest), an unofficial CLI for speed.cloudflare.com | yes | no |
| `ookla` | Ookla's official [Speedtest CLI](https://www.speedtest.net/apps/cli) | no, downloaded on first use | when the CLI can measure it |

The engines use different servers and methods, so their numbers are not comparable. If you switch, results from the old engine stay in the baseline window (seven days) and mix with the new ones until they age out, which can trigger or hide baseline alerts in the meantime.

Where packet loss is not reported, the value is empty, Prometheus exposes `NaN`, and a packet-loss threshold never fires.

### Cloudflare

Download and upload are the median of the largest payload size that produced samples. Small payloads are skipped for the headline number because TCP slow start dominates them. Latency is the median of the latency samples, and jitter is the mean difference between consecutive samples, so a single spike raises jitter noticeably. A run can take a minute or more on a slow link. `speedtest.cloudflare.timeoutSeconds` (default 300) is the limit on its duration.

### Ookla and its license

Ookla's Speedtest CLI is proprietary software, and its [EULA](https://www.speedtest.net/about/eula) puts real limits on who may use it and how. Read it before you enable this engine. Among other things it:

- licenses the CLI for personal, non-commercial use on a single personal computer you own or control,
- forbids redistributing it, including to other people or on a network where several devices can reach it,
- forbids installing it on routers, modems, and other non-personal devices.

Because of the redistribution ban, the Wanetra image does not contain the Ookla binary. That keeps the project's public image on GHCR from distributing it.

Instead, when you opt in, your own Wanetra instance downloads the CLI directly from Ookla (`install.speedtest.net`) the first time a test runs. You receive the software from Ookla, under Ookla's license, on your own machine.

To opt in, accept the license in the web UI Settings page (which records the acceptance timestamp) or set the environment variable:

```sh
SpeedTest__Engine=ookla
SpeedTest__Ookla__AcceptLicense=true
```

Accepting the license states three things on your behalf: that you have read and accept Ookla's [EULA](https://www.speedtest.net/about/eula), [Terms of Use](https://www.speedtest.net/about/terms) and [Privacy Policy](https://www.ookla.com/privacy), that your use qualifies under them, and that Wanetra may download the CLI for you. Wanetra also passes `--accept-license --accept-gdpr` to the CLI, because it refuses to run non-interactively otherwise. If `ookla` is selected without license acceptance, Wanetra refuses to start (when configured via environment) or rejects the setting change with an error.

The Wanetra maintainers cannot grant or confirm that your use is allowed, and this is not legal advice. If you use Wanetra for a business, for a client, or on shared infrastructure, assume the personal-use limit applies to you and choose `librespeed` or `cloudflare`, or get a license from Ookla.

Each Ookla test also sends its result to Ookla, which stores it and issues a result URL.

How the download works:

- Wanetra downloads version 1.2.0 for linux/amd64 or linux/arm64 and checks it against a pinned SHA-256. If the checksum does not match, the file is discarded and the test fails.
- The CLI is stored in `$WANETRA_DATA_PATH/ookla/1.2.0/` and downloaded once. Later tests, including after a restart, reuse it.
- The first test needs a connection to Ookla and takes a moment longer. The download times out after 30 seconds.

Fallback: if the download fails (the host is offline, Ookla has moved the file, the checksum does not match, or the data directory is not writable), Wanetra uses the binary at `SpeedTest__Ookla__ExecutablePath`, if you set one, and logs a warning. Install that binary yourself and mount it into the container, for example read-only at `/usr/local/bin/speedtest`. Without a fallback, the test fails with the download error. While the download keeps failing, each test tries it again before using the fallback, so a host that is permanently offline pays a failed attempt, up to 30 seconds, on every run.

## Alerts and notifications

A fresh install has one alert rule: download falling 30% below the baseline. You can also set minimum download and upload, maximum latency, jitter and packet loss, and a drop for upload.

- The baseline is the median download and upload of the successful results from the previous seven days. It exists only once there are at least ten of them. Until then, the baseline-drop thresholds do nothing.
- Three consecutive bad measurements open an incident, and two good ones close it. A bad measurement is a threshold violation, an unusable result, or a network failure. Both counts are configurable.
- Failures of the local tool (missing binary, timeout, crash) and cancelled runs do not count. They say nothing about your connection. Every result has a `failureKind` that tells these cases apart.
- Disabling the rule closes an open incident with the reason `disabled` and clears the degradation gauge. Enabling it again does not reopen that incident.

ntfy and webhook destinations are copied into an incident when it opens. Delivery is tracked and retried per destination, so a provider that already got the message is not sent it again when another one fails. The recovery notice goes to a destination only after its opening message was delivered. If a provider accepted a request but Wanetra stopped before recording it, that provider can receive a duplicate.

Reading the notification settings never returns secrets or webhook URLs. When you save, leave a field blank to keep its stored value.

## Metrics

`/metrics` serves Prometheus metrics: the latest `wanetra_download_mbps`, `wanetra_upload_mbps`, `wanetra_latency_ms`, `wanetra_jitter_ms` and `wanetra_packet_loss_percent`, plus `wanetra_speedtest_success`, `wanetra_speedtest_duration_seconds`, `wanetra_last_speedtest_timestamp`, `wanetra_connection_degraded`, the counters `wanetra_speedtests_total` and `wanetra_speedtest_failures_total`, and standard HTTP metrics.

## Security

Wanetra has no login and no user accounts. Anyone who can reach the web UI or API can modify application settings, change the speed-test engine, and accept the Ookla license. Keep Wanetra bound to loopback or place it behind an authenticating reverse proxy before exposing it to other users or networks.

The Compose files bind to `127.0.0.1` by default so that only the Docker host can reach it. To use it from your LAN, set `WANETRA_BIND_ADDRESS` to the host's LAN address (or to `0.0.0.0` for every interface) and restrict access with the host firewall. Do not expose it to the public internet without authentication.

Executable paths cannot be modified through the API or web UI for this reason. They must be set through environment variables.

The SQLite database in `/data` stores notification settings as plain JSON, including ntfy tokens and passwords and webhook headers. Limit who can read the volume, and treat its backups as secrets.
## API

| Method | Path | Purpose |
| --- | --- | --- |
| `POST` | `/api/speedtests/run` | Start a manual test. Returns `202` at once, or `409` if one is already running. |
| `GET` | `/api/speedtests/status` | Execution state: `idle`, `running`, or `failed`. |
| `GET` | `/api/speedtests/latest` | Most recent result. |
| `GET` | `/api/speedtests/{id}` | One result. |
| `GET` | `/api/speedtests` | Paged history. |
| `GET` | `/api/schedule` | Current schedule and the next 5 runs. |
| `PUT` | `/api/schedule` | Save `{enabled, cronExpression, timezone}`. An invalid cron expression or timezone returns `400`. |
| `GET` | `/api/schedule/next-runs?count=` | Upcoming runs in UTC (`count` 1 to 100, default 5). Empty when the schedule is off. |
| `GET` | `/api/baseline` | Seven-day rolling download and upload baselines. |
| `GET` | `/api/alerts/active` | The open degradation event, or `404` when there is none. |
| `GET` | `/api/alerts/events?count=` | Recent degradation events (1 to 100, default 20). |
| `GET`, `PUT` | `/api/alerts/rule` | Read or save the alert rule and its transition counts. |
| `GET`, `PUT` | `/api/notifications` | Read or save the ntfy and webhook configuration. |
| `POST` | `/api/notifications/test` | Send a test notification to a saved or draft destination. |
| `GET`, `PUT` | `/api/settings` | Read all settings or atomically update values. |
| `GET` | `/metrics` | Prometheus scrape endpoint. |
| `GET` | `/health`, `/health/live`, `/health/ready` | Health checks. |

A test outlives the request that starts it, so poll `/api/speedtests/status` instead of waiting on the response.

History accepts `from` and `to` (ISO 8601, UTC), `success`, `engine`, `sort` (`asc` or `desc`, default `desc`), `page` (default 1) and `pageSize` (default 50, maximum 200).

The schedule is disabled by default and set to `*/30 * * * *` in `Europe/Istanbul`. Saving it takes effect immediately, with no restart. A run whose time has already passed is skipped and never caught up.

Errors are returned as `{"code": "...", "message": "..."}`.

## Development

You need the .NET 10 SDK and Bun.

```sh
dotnet restore backend/Wanetra.slnx
dotnet build backend/Wanetra.slnx
dotnet test backend/Wanetra.slnx

cd frontend
bun install --frozen-lockfile
bun run lint
bun run test
bun run build
bun run dev
```

The backend is an ASP.NET Core minimal API with Entity Framework Core on SQLite. The frontend is React with TypeScript, Vite, Tailwind CSS and shadcn/ui. See [CONTRIBUTING.md](CONTRIBUTING.md) before opening a pull request, and [docs/RELEASING.md](docs/RELEASING.md) for how releases are cut.

## License

License selection is still open. The bundled LibreSpeed CLI and cfspeedtest keep their own licenses, which are copied to `/usr/share/licenses/` in the image.
