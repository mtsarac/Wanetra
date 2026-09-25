using Prometheus;
using Wanetra.Domain;

namespace Wanetra.Api.Prometheus;

public sealed class WanetraMetrics : IPrometheusMetrics
{
    private static readonly Gauge DownloadMbps = Metrics.CreateGauge("wanetra_download_mbps", "Latest successful download speed in Mbps.");
    private static readonly Gauge UploadMbps = Metrics.CreateGauge("wanetra_upload_mbps", "Latest successful upload speed in Mbps.");
    private static readonly Gauge LatencyMs = Metrics.CreateGauge("wanetra_latency_ms", "Latest successful latency in milliseconds.");
    private static readonly Gauge JitterMs = Metrics.CreateGauge("wanetra_jitter_ms", "Latest successful jitter in milliseconds.");
    private static readonly Gauge PacketLossPercent = Metrics.CreateGauge("wanetra_packet_loss_percent", "Latest successful packet loss percentage.");
    private static readonly Gauge SpeedTestSuccess = Metrics.CreateGauge("wanetra_speedtest_success", "Whether the latest speed test succeeded (1 or 0).");
    private static readonly Gauge SpeedTestDurationSeconds = Metrics.CreateGauge("wanetra_speedtest_duration_seconds", "Duration of the latest speed test in seconds.");
    private static readonly Gauge ConnectionDegraded = Metrics.CreateGauge("wanetra_connection_degraded", "Whether a degradation event is active (1 or 0).");
    private static readonly Gauge LastSpeedTestTimestamp = Metrics.CreateGauge("wanetra_last_speedtest_timestamp", "Unix timestamp of the latest speed test.");
    private static readonly Counter SpeedTestsTotal = Metrics.CreateCounter("wanetra_speedtests_total", "Total number of completed speed tests.");
    private static readonly Counter SpeedTestFailuresTotal = Metrics.CreateCounter("wanetra_speedtest_failures_total", "Total number of failed speed tests.");

    public WanetraMetrics()
    {
        DownloadMbps.Set(0);
        UploadMbps.Set(0);
        LatencyMs.Set(0);
        JitterMs.Set(0);
        PacketLossPercent.Set(0);
        SpeedTestSuccess.Set(0);
        SpeedTestDurationSeconds.Set(0);
        ConnectionDegraded.Set(0);
        LastSpeedTestTimestamp.Set(0);
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
        SpeedTestsTotal.Inc();
        if (!result.Success)
        {
            SpeedTestFailuresTotal.Inc();
        }
    }

    public void SetConnectionDegraded(bool degraded) => ConnectionDegraded.Set(degraded ? 1 : 0);

    private static void SetLatestMeasurements(SpeedTestResult result)
    {
        Set(DownloadMbps, result.DownloadMbps);
        Set(UploadMbps, result.UploadMbps);
        Set(LatencyMs, result.LatencyMs);
        Set(JitterMs, result.JitterMs);
        Set(PacketLossPercent, result.PacketLossPercent);
    }

    private static void SetLatestResult(SpeedTestResult result)
    {
        SpeedTestSuccess.Set(result.Success ? 1 : 0);
        SpeedTestDurationSeconds.Set((result.DurationMs ?? 0) / 1000d);
        LastSpeedTestTimestamp.Set(new DateTimeOffset(DateTime.SpecifyKind(result.Timestamp, DateTimeKind.Utc)).ToUnixTimeSeconds());
    }


    private static void Set(Gauge gauge, double? value)
    {
        if (value.HasValue)
        {
            gauge.Set(value.Value);
        }
    }
}
