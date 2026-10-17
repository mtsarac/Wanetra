using Prometheus;
using Wanetra.Domain;

namespace Wanetra.Api.Prometheus;

public sealed class WanetraMetrics : IPrometheusMetrics
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
}
