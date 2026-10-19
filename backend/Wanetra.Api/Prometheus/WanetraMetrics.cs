using Prometheus;
using Wanetra.Domain;

namespace Wanetra.Api.Prometheus;

public sealed class WanetraMetrics : IPrometheusMetrics, IProbeMetrics, ILanMetrics
{
    private readonly Gauge downloadMbps;
    private readonly Gauge uploadMbps;
    private readonly Gauge latencyMs;
    private readonly Gauge jitterMs;
    private readonly Gauge packetLossPercent;
    private readonly Gauge speedTestSuccess;
    private readonly Gauge speedTestDurationSeconds;
    private readonly Gauge connectionDegraded;
    private readonly Gauge lastSpeedTestTimestamp;
    private readonly Counter speedTestsTotal;
    private readonly Counter speedTestFailuresTotal;
    private readonly Gauge probeUp;
    private readonly Gauge probeRttMs;
    private readonly Counter probeFailuresTotal;
    private readonly Gauge outageActive;
    private readonly Counter outagesTotal;
    private readonly Gauge lanDevices;
    private readonly Gauge lanDevicesOnline;
    private readonly Gauge lanDevicesUnknown;
    private readonly Gauge lanLastScanTimestamp;
    private readonly Gauge lanLastScanSuccess;
    private readonly Counter lanScansTotal;
    private readonly Lock probeGate = new();
    private readonly Dictionary<string, string> probeRoles = new(StringComparer.Ordinal);

    // Owned per instance so hosts in the same process (tests) never share gauge state.
    public CollectorRegistry Registry { get; } = Metrics.NewCustomRegistry();

    public WanetraMetrics()
    {
        var factory = Metrics.WithCustomRegistry(Registry);
        downloadMbps = factory.CreateGauge("wanetra_download_mbps", "Latest successful download speed in Mbps.");
        uploadMbps = factory.CreateGauge("wanetra_upload_mbps", "Latest successful upload speed in Mbps.");
        latencyMs = factory.CreateGauge("wanetra_latency_ms", "Latest successful latency in milliseconds.");
        jitterMs = factory.CreateGauge("wanetra_jitter_ms", "Latest successful jitter in milliseconds.");
        packetLossPercent = factory.CreateGauge("wanetra_packet_loss_percent", "Latest successful packet loss percentage.");
        speedTestSuccess = factory.CreateGauge("wanetra_speedtest_success", "Whether the latest speed test succeeded (1 or 0).");
        speedTestDurationSeconds = factory.CreateGauge("wanetra_speedtest_duration_seconds", "Duration of the latest speed test in seconds.");
        connectionDegraded = factory.CreateGauge("wanetra_connection_degraded", "Whether a degradation event is active (1 or 0).");
        lastSpeedTestTimestamp = factory.CreateGauge("wanetra_last_speedtest_timestamp", "Unix timestamp of the latest speed test.");
        speedTestsTotal = factory.CreateCounter("wanetra_speedtests_total", "Total number of completed speed tests.");
        speedTestFailuresTotal = factory.CreateCounter("wanetra_speedtest_failures_total", "Total number of failed speed tests.");

        probeUp = factory.CreateGauge("wanetra_probe_up", "Whether the latest probe of a target succeeded (1 or 0).", ["target", "role"]);
        probeRttMs = factory.CreateGauge("wanetra_probe_rtt_ms", "Round-trip time of the latest probe of a target in milliseconds, NaN when it failed.", ["target", "role"]);
        probeFailuresTotal = factory.CreateCounter("wanetra_probe_failures_total", "Total number of failed probes per target.", ["target", "role"]);
        outageActive = factory.CreateGauge("wanetra_outage_active", "Whether a connection outage is open (1 or 0).");
        outagesTotal = factory.CreateCounter("wanetra_outages_total", "Total number of connection outages opened.");

        lanDevices = factory.CreateGauge("wanetra_lan_devices", "Devices Wanetra has seen on the local network.");
        lanDevicesOnline = factory.CreateGauge("wanetra_lan_devices_online", "Devices that answered the latest LAN scan.");
        lanDevicesUnknown = factory.CreateGauge("wanetra_lan_devices_unknown", "Devices that have not been marked as known.");
        lanLastScanTimestamp = factory.CreateGauge("wanetra_lan_last_scan_timestamp", "Unix timestamp of the latest LAN scan.");
        lanLastScanSuccess = factory.CreateGauge("wanetra_lan_last_scan_success", "Whether the latest LAN scan succeeded (1 or 0).");
        lanScansTotal = factory.CreateCounter("wanetra_lan_scans_total", "Total number of LAN scans.");

        downloadMbps.Set(0);
        uploadMbps.Set(0);
        latencyMs.Set(0);
        jitterMs.Set(0);
        packetLossPercent.Set(double.NaN);
        speedTestSuccess.Set(0);
        speedTestDurationSeconds.Set(0);
        connectionDegraded.Set(0);
        lastSpeedTestTimestamp.Set(0);
    }

    public void Initialize(
        SpeedTestResult? latestResult,
        SpeedTestResult? latestSuccessfulResult,
        bool connectionDegraded)
    {
        if (latestSuccessfulResult is not null)
        {
            SetLatestMeasurements(latestSuccessfulResult);
        }

        if (latestResult is not null)
        {
            SetLatestResult(latestResult);
        }

        SetConnectionDegraded(connectionDegraded);
    }

    public void RecordSpeedTest(SpeedTestResult result)
    {
        if (result.Success)
        {
            SetLatestMeasurements(result);
        }

        SetLatestResult(result);
        speedTestsTotal.Inc();
        if (!result.Success)
        {
            speedTestFailuresTotal.Inc();
        }
    }

    public void SetConnectionDegraded(bool degraded) => connectionDegraded.Set(degraded ? 1 : 0);

    private void SetLatestMeasurements(SpeedTestResult result)
    {
        Set(downloadMbps, result.DownloadMbps);
        Set(uploadMbps, result.UploadMbps);
        Set(latencyMs, result.LatencyMs);
        Set(jitterMs, result.JitterMs);
        packetLossPercent.Set(result.PacketLossPercent ?? double.NaN);
    }

    private void SetLatestResult(SpeedTestResult result)
    {
        speedTestSuccess.Set(result.Success ? 1 : 0);
        speedTestDurationSeconds.Set((result.DurationMs ?? 0) / 1000d);
        lastSpeedTestTimestamp.Set(new DateTimeOffset(DateTime.SpecifyKind(result.Timestamp, DateTimeKind.Utc)).ToUnixTimeSeconds());
    }

    private static void Set(Gauge gauge, double? value)
    {
        if (value.HasValue)
        {
            gauge.Set(value.Value);
        }
    }

    public void RecordProbe(string target, string role, bool success, double? rttMs)
    {
        lock (probeGate)
        {
            if (probeRoles.TryGetValue(target, out var knownRole) && knownRole != role)
            {
                RemoveSeries(target, knownRole);
            }

            probeRoles[target] = role;
        }

        probeUp.WithLabels(target, role).Set(success ? 1 : 0);
        probeRttMs.WithLabels(target, role).Set(success && rttMs.HasValue ? rttMs.Value : double.NaN);
        if (!success)
        {
            probeFailuresTotal.WithLabels(target, role).Inc();
        }
    }

    public void RemoveProbeTarget(string target)
    {
        lock (probeGate)
        {
            if (probeRoles.Remove(target, out var role))
            {
                RemoveSeries(target, role);
            }
        }
    }

    public void SetOutageActive(bool active) => outageActive.Set(active ? 1 : 0);

    public void RecordOutageOpened() => outagesTotal.Inc();

    private void RemoveSeries(string target, string role)
    {
        probeUp.RemoveLabelled(target, role);
        probeRttMs.RemoveLabelled(target, role);
        probeFailuresTotal.RemoveLabelled(target, role);
    }

    public void SetDeviceCounts(int total, int online, int unknown)
    {
        lanDevices.Set(total);
        lanDevicesOnline.Set(online);
        lanDevicesUnknown.Set(unknown);
    }

    public void RecordScan(bool success, double durationSeconds)
    {
        lanScansTotal.Inc();
        lanLastScanSuccess.Set(success ? 1 : 0);
        lanLastScanTimestamp.Set(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
    }
}
