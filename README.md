# Wanetra

[![CI](https://github.com/mtsarac/Wanetra/actions/workflows/ci.yml/badge.svg)](https://github.com/mtsarac/Wanetra/actions/workflows/ci.yml)
[![Release](https://img.shields.io/github/v/release/mtsarac/Wanetra)](https://github.com/mtsarac/Wanetra/releases/latest)
[![License](https://img.shields.io/github/license/mtsarac/Wanetra)](LICENSE)

Wanetra is a self-hosted monitor for your internet connection. It probes the connection every few seconds to catch outages and tell you where they are, runs speed tests on a schedule, keeps the history in SQLite, shows it in a web dashboard, and tells you through ntfy or a webhook when your connection degrades and when it recovers. It also exposes Prometheus metrics. It runs as a single Docker container.

It is pre-1.0. The API and the database schema can still change between releases, so back up the data volume before upgrading.

## Features

- Continuous connection monitoring: ping, TCP, DNS and HTTP probes of your gateway, public IPs and DNS, with outage detection that says whether the router, the ISP or DNS is at fault
- Uptime over 24 hours, 7 days and 30 days, a latency and packet-loss chart, and an outage timeline
- Local network inventory: finds the devices on your LAN, names them, flags unknown newcomers and watches the ones you care about (Linux hosts, opt-in)
- Speed tests with one of three engines: LibreSpeed, Cloudflare, or Ookla
- Manual runs and cron schedules with a timezone, one test at a time
- History with filtering and paging, plus a seven-day rolling baseline
- Degradation alerts (thresholds and drops against the baseline) and outage alerts, with recovery notices, sent through ntfy or a generic webhook
- Prometheus metrics at `/metrics`
- Sign-in with a single account, created on first start; it can be turned off again
- Automatic cleanup of old results (365 days by default)

## Quick start

You need Docker with Compose.

```sh
git clone https://github.com/mtsarac/Wanetra.git
cd Wanetra
docker compose up -d
```

The dashboard is at `http://127.0.0.1:8080` on the Docker host. On the first visit Wanetra asks you to create an account. It prints a one-time setup code in its log when it starts (`docker compose logs wanetra`); enter that code with the username and password you want. The scheduler is off by default, so press **Run speed test** or enable a schedule under Settings.

The Compose file pulls `ghcr.io/mtsarac/wanetra:latest` (linux/amd64 and linux/arm64) and keeps data in a Docker-managed volume named `wanetra-data`, mounted at `/data`. No `.env` file or host directory is needed.

To build the image from source instead, run `docker compose -f compose.example.yaml up -d --build`.

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

Executable paths are restricted (`speedtest.librespeed.executablePath`, `speedtest.cloudflare.executablePath`, `speedtest.ookla.executablePath`). Editable binary paths would let anyone with a session run arbitrary commands, and sign-in can be turned off. Restricted settings cannot be changed through the UI or API; they can only be set via environment variables.

Common configuration variables:

| Variable | Default | Purpose |
| --- | --- | --- |
| `WANETRA_DATA_PATH` | `/data` | Directory for the database and downloaded tools |
| `WANETRA_PORT` | `8080` | Host port published by the Compose files |
| `WANETRA_BIND_ADDRESS` | `127.0.0.1` | Host interface published by the Compose files |
| `WANETRA_VERSION` | `latest` | Image tag used by `compose.yaml` |
| `DataRetention__Days` | `365` | Days of speed-test history, hourly probe summaries and outages to keep |
| `Probes__Enabled` | `true` | Turns connection monitoring on or off |
| `Probes__IntervalSeconds` | `10` | Seconds between probe rounds (5 to 300) |
| `Lan__Enabled` | `false` (`true` in `compose.lan.yaml`) | Turns local network scanning on or off |
| `Lan__Cidr` | empty | Networks to scan, for example `192.168.1.0/24`. Empty detects this machine's networks |
| `SpeedTest__Engine` | `librespeed` | Active engine: `librespeed`, `cloudflare`, or `ookla` |
| `SpeedTest__Ookla__AcceptLicense` | `false` | Required for `ookla`. Read [Ookla and its license](#ookla-and-its-license) first |
| `SpeedTest__Ookla__ExecutablePath` | empty | Optional fallback binary for `ookla` |
| `SpeedTest__Cloudflare__TimeoutSeconds` | `300` | Run time limit for `cloudflare`, in seconds |
| `SpeedTest__Cloudflare__ExecutablePath` | `cfspeedtest` | Binary used by `cloudflare` |
| `WANETRA_AUTH` | empty | `disabled` turns sign-in off, `enabled` forces it on. Empty lets the Settings page decide. See [Security](#security) |
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

## Connection monitoring

A speed test every half hour cannot see a three-minute outage. Wanetra therefore also probes the connection every 10 seconds. Open **Connectivity** in the UI to see the live state, uptime, a latency chart and the outage timeline. The Dashboard shows a short version.

A new install probes five targets:

| Target | Check | Counts as |
| --- | --- | --- |
| Gateway | ping to the default gateway, detected from the routing table | gateway |
| Cloudflare 1.1.1.1, Google 8.8.8.8, Quad9 9.9.9.9 | ping | internet |
| DNS lookup | resolve `cloudflare.com` with the system resolver | dns |

You can add, pause, edit and delete targets on the Connectivity page (up to 32). A target can ping a host, open a TCP port, resolve a name (optionally through a specific resolver such as `1.1.1.1`) or fetch an HTTP URL. `custom` targets show up in charts and metrics but never raise an outage.

### How an outage is decided

Each round, a layer is *up* when any of its targets answered and *down* when every one failed. One flaky target therefore never counts.

| Gateway | Internet | DNS | Result |
| --- | --- | --- | --- |
| down | down | any | **Local network** outage: the router or the link to it |
| up or unknown | down | any | **Internet** outage: the router answers but the ISP link does not |
| any | up | down | **DNS** outage |

A router that ignores ping while the internet works is not an outage.

Three bad rounds in a row open an outage, dated from the first bad round, and three good rounds close it, dated from the first good round. Both counts are settings. Failures during a speed test do not count, because the test saturates the link; the samples are still stored. Scheduled speed tests are skipped while an outage is open, so they do not fail for a reason you already know. If Wanetra itself restarts during an outage, the outage is closed as `interrupted` at the last time it was seen.

Outage notices use the same ntfy and webhook destinations as degradation alerts. A notice that cannot be delivered because the connection is down stays queued and is retried every minute, so you get "internet outage, 14:02 to 14:19 (17m)" when the connection comes back. A webhook or ntfy server on your LAN is notified immediately.

### Storage

Every probe is stored for 48 hours, summarized per minute for 30 days and per hour for as long as `DataRetention__Days` (365). Outages are kept for `DataRetention__Days`. All of these are settings under Settings → Connection monitoring. Uptime is computed from the recorded outages, and it only covers the time monitoring has been running.

### Ping inside Docker

Wanetra runs as a non-root user and pings with the `ping` utility in the image, which works when the kernel lets that user open ICMP sockets. The Compose files allow it with `net.ipv4.ping_group_range`; with host networking (`compose.lan.yaml`) a container cannot set that sysctl, so set it on the host. If ICMP is still not available, Wanetra logs a warning and falls back to TCP checks on ports 443 and 80, where a connection or a reset both count as "reachable". Under bridge networking the detected gateway is the Docker bridge address of the host, not your router.

## Local network scanning

Open **Devices** to see what is on your LAN. Wanetra keeps an inventory keyed by MAC address: IP address, a name (from mDNS, NetBIOS, reverse DNS or UPnP), the manufacturer, when it was last seen and what changed. Scanning is off until you turn it on under Settings → Network scanning, and it needs the host's network, so use the extra Compose file:

```sh
docker compose -f compose.lan.yaml up -d
```

Docker's default bridge network hides the LAN from a container, and `compose.lan.yaml` fixes that with `network_mode: host`. That means Linux only (Docker Desktop on macOS and Windows runs containers in a VM). There is no port mapping any more: Wanetra listens on `WANETRA_BIND_ADDRESS:WANETRA_PORT` of the host, `127.0.0.1:8080` by default. If you set the address to `0.0.0.0` to reach the UI from another machine, the UI is open to your whole network, so keep sign-in on. The file also replaces the container health check, which would otherwise keep asking port 8080.

### How it works

Wanetra needs no extra privileges and no raw sockets:

1. It sends one small UDP datagram to every address in the network. For an address on the same network the kernel first asks "who has this IP?" (ARP).
2. After about ten seconds it reads the kernel's neighbour table (`/proc/net/arp`). Whoever answered is there, even devices that ignore ping.
3. For each device that answered it asks for a name through mDNS and NetBIOS, falls back to reverse DNS, and listens for UPnP announcements (routers, TVs, printers). The manufacturer comes from the IEEE registry built into the image.

Only private networks are scanned (`10.0.0.0/8`, `172.16.0.0/12`, `192.168.0.0/16` and link-local), at most 1,022 addresses at a time. Without `Lan__Cidr`, Wanetra scans the network of each real interface on the host, ignoring loopback, VPN and Docker interfaces. Larger subnets are narrowed to the `/24` the host sits in. A container on Docker's bridge network is detected and told to use `compose.lan.yaml`.

### What you get

- **First scan** learns the network and marks everything as known, so you are not alerted about your own things.
- **Unknown devices**: anything that shows up later is flagged *New* and raises a notification (turn that off with `Lan__AlertOnNewDevice`). Check it, then mark it as yours. **Mark all as known** clears the list after you have reviewed it.
- **Offline**: a device is offline after it misses three scans in a row (`Lan__OfflineAfterScans`), because phones sleep. A device that flickers never flips state.
- **Watched devices** raise a notification when they go offline and another when they return, and get a ping target on the Connectivity page, which follows the device if DHCP gives it a new address.
- **History** of joins, offline and online changes and address changes per device.
- **Private MAC addresses**: phones and laptops invent a different MAC per network, which cannot be told apart from a new device, and their manufacturer is hidden. They are labelled *Private MAC*; give them a name and mark them as known.

The scan result is only as good as the network allows: devices on another VLAN or behind a guest-network client isolation are not visible, and a device that has been unplugged can stay in the kernel's table for a short while.

The manufacturer table is generated by `bun scripts/update-oui.ts` from the IEEE registry. Re-run it and commit `backend/Wanetra.Infrastructure/Lan/oui.txt` to refresh it.

## Alerts and notifications

A fresh install has one alert rule: download falling 30% below the baseline. You can also set minimum download and upload, maximum latency, jitter and packet loss, and a drop for upload.

- The baseline is the median download and upload of the successful results from the previous seven days. It exists only once there are at least ten of them. Until then, the baseline-drop thresholds do nothing.
- Three consecutive bad measurements open an incident, and two good ones close it. A bad measurement is a threshold violation, an unusable result, or a network failure. Both counts are configurable.
- Failures of the local tool (missing binary, timeout, crash) and cancelled runs do not count. They say nothing about your connection. Every result has a `failureKind` that tells these cases apart.
- Disabling the rule closes an open incident with the reason `disabled` and clears the degradation gauge. Enabling it again does not reopen that incident.

ntfy and webhook destinations are copied into an incident when it opens. Delivery is tracked and retried per destination, so a provider that already got the message is not sent it again when another one fails. The recovery notice goes to a destination only after its opening message was delivered. If a provider accepted a request but Wanetra stopped before recording it, that provider can receive a duplicate.

Reading the notification settings never returns secrets or webhook URLs. When you save, leave a field blank to keep its stored value.

## Metrics

`/metrics` serves Prometheus metrics: the latest `wanetra_download_mbps`, `wanetra_upload_mbps`, `wanetra_latency_ms`, `wanetra_jitter_ms` and `wanetra_packet_loss_percent`, plus `wanetra_speedtest_success`, `wanetra_speedtest_duration_seconds`, `wanetra_last_speedtest_timestamp`, `wanetra_connection_degraded`, the counters `wanetra_speedtests_total` and `wanetra_speedtest_failures_total`, the connection-monitoring metrics `wanetra_probe_up{target,role}`, `wanetra_probe_rtt_ms{target,role}` (`NaN` when a probe failed), `wanetra_probe_failures_total{target,role}`, `wanetra_outage_active` and `wanetra_outages_total`, the scanning metrics `wanetra_lan_devices`, `wanetra_lan_devices_online`, `wanetra_lan_devices_unknown`, `wanetra_lan_scans_total`, `wanetra_lan_last_scan_success` and `wanetra_lan_last_scan_timestamp`, and standard HTTP metrics.

## Security

Wanetra has a single account. The first time you open the UI it asks you to create it, and proves you are the owner with a one-time setup code that it writes to its log when it starts. Until the account exists, every `/api` route answers `401`, so nobody who reaches the port first can claim the instance without log access. If you restart before finishing setup, a new code is printed.

After that:

- The browser gets an `HttpOnly`, `SameSite=Strict` session cookie that lasts 30 days. Passwords are stored as PBKDF2-SHA-256 hashes. Only a hash of each session token is stored.
- Five failed sign-ins from one address in 15 minutes lock that address out for the rest of the window (`429` with `Retry-After`).
- State-changing API calls made with the cookie must carry an `X-Wanetra-Request` header. The web UI sends it. Scripts that reuse a session cookie have to send it too.
- `/health`, `/health/live` and `/health/ready` stay open so the Docker health check keeps working.
- `/metrics` needs either a session or a bearer token. Generate the token under **Settings → Security**; it is shown once, and generating a new one replaces the old one. Point Prometheus at it with `authorization: { credentials: <token> }`. After upgrading from a version without authentication, Prometheus gets `401` until you add the token.
- The cookie only carries the `Secure` flag when the request arrived over HTTPS. Behind a TLS-terminating reverse proxy, set `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` so Wanetra sees the original scheme.

You can turn authentication off under **Settings → Security**, after confirming your password, and turn it on again later. If you lose the password, start the container with `WANETRA_AUTH=disabled`, then set a new password under Settings or turn authentication back on. While `WANETRA_AUTH` is set, Settings cannot change authentication, the same way other environment variables lock their settings. `WANETRA_AUTH=enabled` pins it on.

The device inventory (every MAC and IP address on your network) is sensitive. With host networking Wanetra listens on the host, so keep sign-in on and the bind address on `127.0.0.1` unless you need more.

Authentication is not a substitute for network isolation: the connection is plain HTTP unless you add a reverse proxy with TLS. The Compose files bind to `127.0.0.1` by default so that only the Docker host can reach it. To use it from your LAN, set `WANETRA_BIND_ADDRESS` to the host's LAN address (or to `0.0.0.0` for every interface) and put TLS in front of it if the network is not trusted. Do not expose it to the public internet without a reverse proxy.

Executable paths cannot be modified through the API or web UI. They must be set through environment variables.

The SQLite database in `/data` stores notification settings as plain JSON, including ntfy tokens and passwords and webhook headers. Limit who can read the volume, and treat its backups as secrets.

To report a vulnerability, follow [SECURITY.md](SECURITY.md).

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
| `GET`, `POST` | `/api/probes/targets` | List probe targets or add one `{name, kind, role, target, port, resolver, enabled}`. `kind` is `icmp`, `tcp`, `dns` or `http`; `role` is `gateway`, `internet`, `dns` or `custom`. |
| `PUT`, `DELETE` | `/api/probes/targets/{id}` | Replace or delete a target. Deleting removes its samples. |
| `GET` | `/api/probes/status` | Live state (`healthy`, `degraded`, `outage`, `unknown`, `disabled`), the state of each layer, every target's latest result and the open outage. |
| `GET` | `/api/probes/series?targetId=&from=&to=&resolution=` | Latency and loss for one target. `resolution` is `auto` (default), `raw` (24 hours at most), `minute` or `hour`. |
| `GET` | `/api/outages?count=` | Recent outages (1 to 100, default 20). |
| `GET` | `/api/uptime?range=` | Uptime for `24h` (default), `7d`, `30d` or `90d`. |
| `GET` | `/api/lan/status` | Whether scanning is on, whether a scan is running, the next scan, the last scan, device counts and the networks being scanned. |
| `POST` | `/api/lan/scan` | Start a scan. `202`, or `409` when scanning is off (`lan_disabled`) or a scan is already running. |
| `GET` | `/api/lan/events?count=` | Recent joins, offline and online changes and address changes across all devices (1 to 200, default 50). |
| `GET` | `/api/devices` | Every device seen. |
| `GET`, `PUT`, `DELETE` | `/api/devices/{id}` | Read a device, change `{name, known, watch}` (omitted fields keep their value; an empty name clears it) or forget it. |
| `GET` | `/api/devices/{id}/events?count=` | History of one device. |
| `POST` | `/api/devices/mark-all-known` | Mark every device as known. |
| `GET`, `PUT` | `/api/alerts/rule` | Read or save the alert rule and its transition counts. |
| `GET`, `PUT` | `/api/notifications` | Read or save the ntfy and webhook configuration. |
| `POST` | `/api/notifications/test` | Send a test notification to a saved or draft destination. |
| `GET`, `PUT` | `/api/settings` | Read all settings or atomically update values. |
| `GET` | `/api/auth/status` | Whether sign-in is on, whether setup is needed and whether this session is signed in. Open to everyone. |
| `POST` | `/api/auth/setup` | Create the account with `{setupCode, username, password}`. Only while no account exists. |
| `POST` | `/api/auth/login`, `/api/auth/logout` | Start or end a session. |
| `POST` | `/api/auth/change-password` | Save `{currentPassword, newPassword}` and sign out other sessions. |
| `POST` | `/api/auth/disable`, `/api/auth/enable` | Turn sign-in off (needs `{password}`) or on. `409` while `WANETRA_AUTH` is set. |
| `POST`, `DELETE` | `/api/auth/metrics-token` | Create (and return once) or revoke the `/metrics` bearer token. |
| `GET` | `/metrics` | Prometheus scrape endpoint. Needs a session or the bearer token. |
| `GET` | `/health`, `/health/live`, `/health/ready` | Health checks. |

With authentication on, every `/api` route except `/api/auth/status`, `/api/auth/setup`, `/api/auth/login` and `/api/auth/logout` needs a session.

A test outlives the request that starts it, so poll `/api/speedtests/status` instead of waiting on the response.

A machine-readable OpenAPI 3 description of the API is served at `/openapi/v1.json`.

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

Wanetra is licensed under the [GNU Affero General Public License v3.0](LICENSE). The bundled LibreSpeed CLI and cfspeedtest keep their own licenses, which are copied to `/usr/share/licenses/` in the image.
