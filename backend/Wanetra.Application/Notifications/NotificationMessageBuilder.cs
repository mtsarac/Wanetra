using System.Globalization;
using Wanetra.Domain;

namespace Wanetra.Application.Notifications;

public static class NotificationMessageBuilder
{
    public static NotificationMessage Build(NotificationTrigger trigger, DegradationEvent degradationEvent) =>
        trigger switch
        {
            NotificationTrigger.Opened => BuildOpened(degradationEvent),
            NotificationTrigger.Recovered => BuildRecovered(degradationEvent),
            _ => throw new ArgumentOutOfRangeException(nameof(trigger), trigger, null),
        };

    public static NotificationMessage Test(string provider) => new(
        "Wanetra test notification",
        $"Test notification from Wanetra via {provider}.",
        "connection.test",
        DateTime.UtcNow,
        null,
        null,
        null);

    private static NotificationMessage BuildOpened(DegradationEvent degradationEvent)
    {
        var lines = new List<string> { "Internet degradation detected", string.Empty };
        AddMetricLine(lines, "Download", degradationEvent.BaselineDownloadMbps, degradationEvent.WorstDownloadMbps, "Mbps");
        AddMetricLine(lines, "Upload", degradationEvent.BaselineUploadMbps, degradationEvent.WorstUploadMbps, "Mbps");
        AddMaxLine(lines, "Latency", degradationEvent.MaxLatencyMs, "ms");
        AddMaxLine(lines, "Jitter", degradationEvent.MaxJitterMs, "ms");
        AddMaxLine(lines, "Packet loss", degradationEvent.MaxPacketLossPercent, "%");

        return new NotificationMessage(
            "Internet degradation detected",
            string.Join('\n', lines),
            "connection.degraded",
            DateTime.UtcNow,
            degradationEvent.WorstDownloadMbps,
            degradationEvent.BaselineDownloadMbps,
            PercentDrop(degradationEvent.BaselineDownloadMbps, degradationEvent.WorstDownloadMbps));
    }

    private static NotificationMessage BuildRecovered(DegradationEvent degradationEvent)
    {
        var duration = degradationEvent.EndedAt.HasValue
            ? degradationEvent.EndedAt.Value - degradationEvent.StartedAt
            : (TimeSpan?)null;
        var body = duration.HasValue
            ? $"Internet connection recovered after {(int)duration.Value.TotalMinutes}m {duration.Value.Seconds}s."
            : "Internet connection recovered.";

        return new NotificationMessage(
            "Internet connection recovered",
            body,
            "connection.recovered",
            DateTime.UtcNow,
            degradationEvent.WorstDownloadMbps,
            degradationEvent.BaselineDownloadMbps,
            PercentDrop(degradationEvent.BaselineDownloadMbps, degradationEvent.WorstDownloadMbps));
    }

    private static void AddMetricLine(List<string> lines, string label, double? baseline, double? worst, string unit)
    {
        if (baseline is null && worst is null)
        {
            return;
        }

        var drop = PercentDrop(baseline, worst);
        var text = $"{label}:\n{Format(baseline)} → {Format(worst)} {unit}";
        if (drop.HasValue)
        {
            text += $" ({drop.Value.ToString("+0.0;-0.0", CultureInfo.InvariantCulture)}%)";
        }

        lines.Add(text);
        lines.Add(string.Empty);
    }

    private static void AddMaxLine(List<string> lines, string label, double? max, string unit)
    {
        if (max is null)
        {
            return;
        }

        lines.Add($"{label}:\n{Format(max)} {unit}");
        lines.Add(string.Empty);
    }

    private static double? PercentDrop(double? baseline, double? worst) =>
        baseline is > 0 && worst.HasValue
            ? (worst.Value - baseline.Value) / baseline.Value * 100
            : null;

    private static string Format(double? value) =>
        value.HasValue ? value.Value.ToString("0.0", CultureInfo.InvariantCulture) : "—";
}
