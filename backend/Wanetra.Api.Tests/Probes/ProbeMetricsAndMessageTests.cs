using Wanetra.Api.Prometheus;
using Wanetra.Application.Notifications;
using Wanetra.Domain;

namespace Wanetra.Api.Tests.Probes;

public class ProbeMetricsAndMessageTests
{
    private static async Task<string> ExportAsync(WanetraMetrics metrics)
    {
        using var buffer = new MemoryStream();
        await metrics.Registry.CollectAndExportAsTextAsync(buffer);
        return System.Text.Encoding.UTF8.GetString(buffer.ToArray());
    }

    [Fact]
    public async Task Probe_results_are_exposed_per_target()
    {
        var metrics = new WanetraMetrics();

        metrics.RecordProbe("Cloudflare", "internet", success: true, rttMs: 12.5);
        metrics.RecordProbe("Gateway", "gateway", success: false, rttMs: null);

        var text = await ExportAsync(metrics);
        Assert.Contains("wanetra_probe_up{target=\"Cloudflare\",role=\"internet\"} 1", text);
        Assert.Contains("wanetra_probe_rtt_ms{target=\"Cloudflare\",role=\"internet\"} 12.5", text);
        Assert.Contains("wanetra_probe_up{target=\"Gateway\",role=\"gateway\"} 0", text);
        Assert.Contains("wanetra_probe_rtt_ms{target=\"Gateway\",role=\"gateway\"} NaN", text);
        Assert.Contains("wanetra_probe_failures_total{target=\"Gateway\",role=\"gateway\"} 1", text);
        Assert.DoesNotContain("wanetra_probe_failures_total{target=\"Cloudflare\"", text);
    }

    [Fact]
    public async Task A_removed_or_re_roled_target_leaves_no_stale_series()
    {
        var metrics = new WanetraMetrics();
        metrics.RecordProbe("Old", "internet", true, 1);
        metrics.RecordProbe("Moved", "internet", true, 1);

        metrics.RemoveProbeTarget("Old");
        metrics.RecordProbe("Moved", "custom", true, 1);

        var text = await ExportAsync(metrics);
        Assert.DoesNotContain("target=\"Old\"", text);
        Assert.DoesNotContain("target=\"Moved\",role=\"internet\"", text);
        Assert.Contains("wanetra_probe_up{target=\"Moved\",role=\"custom\"} 1", text);
    }

    [Fact]
    public async Task Outages_are_counted_and_the_active_flag_follows()
    {
        var metrics = new WanetraMetrics();
        Assert.Contains("wanetra_outage_active 0", await ExportAsync(metrics));

        metrics.SetOutageActive(true);
        metrics.RecordOutageOpened();

        var text = await ExportAsync(metrics);
        Assert.Contains("wanetra_outage_active 1", text);
        Assert.Contains("wanetra_outages_total 1", text);
    }

    [Theory]
    [InlineData(OutageScope.Wan, "Internet outage")]
    [InlineData(OutageScope.LocalNetwork, "Local network outage")]
    [InlineData(OutageScope.Dns, "DNS outage")]
    public void The_opening_message_names_the_scope_and_the_start(OutageScope scope, string title)
    {
        var outage = new Outage
        {
            StartedAt = new DateTime(2026, 10, 18, 14, 2, 5, DateTimeKind.Utc),
            Scope = scope,
            Reason = "Every internet target is unreachable",
        };

        var message = NotificationMessageBuilder.Build(NotificationTrigger.Opened, outage);

        Assert.Equal(title, message.Title);
        Assert.Equal("connection.outage", message.Event);
        Assert.Contains("Every internet target is unreachable", message.Body);
        Assert.Contains("2026-10-18 14:02:05 UTC", message.Body);
    }

    [Fact]
    public void The_recovery_message_reports_the_duration_and_the_window()
    {
        var outage = new Outage
        {
            StartedAt = new DateTime(2026, 10, 18, 14, 2, 0, DateTimeKind.Utc),
            EndedAt = new DateTime(2026, 10, 18, 14, 19, 12, DateTimeKind.Utc),
            Scope = OutageScope.Wan,
            Reason = "r",
        };

        var message = NotificationMessageBuilder.Build(NotificationTrigger.Recovered, outage);

        Assert.Equal("connection.restored", message.Event);
        Assert.Contains("17m 12s", message.Body);
        Assert.Contains("14:02:00", message.Body);
        Assert.Contains("14:19:12", message.Body);
    }

    [Fact]
    public void Long_outages_are_reported_in_hours()
    {
        var outage = new Outage
        {
            StartedAt = new DateTime(2026, 10, 18, 1, 0, 0, DateTimeKind.Utc),
            EndedAt = new DateTime(2026, 10, 18, 4, 5, 9, DateTimeKind.Utc),
            Scope = OutageScope.Wan,
            Reason = "r",
        };

        Assert.Contains("3h 5m 9s", NotificationMessageBuilder.Build(NotificationTrigger.Recovered, outage).Body);
    }

    [Fact]
    public void An_outage_closed_because_monitoring_stopped_says_so()
    {
        var outage = new Outage
        {
            StartedAt = new DateTime(2026, 10, 18, 14, 0, 0, DateTimeKind.Utc),
            EndedAt = new DateTime(2026, 10, 18, 14, 5, 0, DateTimeKind.Utc),
            ClosureReason = OutageClosureReasons.MonitoringDisabled,
            Scope = OutageScope.Wan,
            Reason = "r",
        };

        Assert.Contains("monitoring was turned off", NotificationMessageBuilder.Build(NotificationTrigger.Recovered, outage).Body, StringComparison.OrdinalIgnoreCase);
    }
}
