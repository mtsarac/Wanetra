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

    public static NotificationMessage Build(NotificationTrigger trigger, Outage outage) =>
        trigger switch
        {
            NotificationTrigger.Opened => BuildOutageOpened(outage),
            NotificationTrigger.Recovered => BuildOutageRecovered(outage),
            _ => throw new ArgumentOutOfRangeException(nameof(trigger), trigger, null),
        };

    public static NotificationMessage Build(NotificationTrigger trigger, DeviceAlert alert) =>
        trigger switch
        {
            NotificationTrigger.Opened => BuildDeviceOpened(alert),
            NotificationTrigger.Recovered => BuildDeviceRecovered(alert),
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

    private static NotificationMessage BuildDeviceOpened(DeviceAlert alert)
    {
        var details = new List<string> { $"MAC: {alert.Mac}" };
        if (alert.Ip is not null)
        {
            details.Add($"IP: {alert.Ip}");
        }

        if (alert.Vendor is not null)
        {
            details.Add($"Vendor: {alert.Vendor}");
        }

        return alert.Kind == DeviceAlertKind.NewDevice
            ? new NotificationMessage(
                "New device on your network",
                $"{alert.DeviceLabel} joined the network.\n\n{string.Join('\n', details)}\n\nMark it as known on the Devices page if it is yours.",
                "device.new",
                DateTime.UtcNow,
                null,
                null,
                null)
            : new NotificationMessage(
                "Watched device offline",
                $"{alert.DeviceLabel} stopped answering.\n\n{string.Join('\n', details)}",
                "device.offline",
                DateTime.UtcNow,
                null,
                null,
                null);
    }

    private static NotificationMessage BuildDeviceRecovered(DeviceAlert alert)
    {
        var body = alert.EndedAt is { } ended
            ? $"{alert.DeviceLabel} is back online after {FormatDuration(ended - alert.StartedAt)}."
            : $"{alert.DeviceLabel} is back online.";
        return new NotificationMessage("Device back online", body, "device.online", DateTime.UtcNow, null, null, null);
    }

    private static NotificationMessage BuildOutageOpened(Outage outage)
    {
        var title = outage.Scope switch
        {
            OutageScope.LocalNetwork => "Local network outage",
            OutageScope.Dns => "DNS outage",
            _ => "Internet outage",
        };
        var body = $"{title} detected\n\n{outage.Reason}.\nStarted: {outage.StartedAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)} UTC";

        return new NotificationMessage(title, body, "connection.outage", DateTime.UtcNow, null, null, null);
    }

    private static NotificationMessage BuildOutageRecovered(Outage outage)
    {
        var started = outage.StartedAt.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
        string body;
        if (outage.EndedAt is { } ended)
        {
            var duration = ended - outage.StartedAt;
            var ending = ended.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
            body = outage.ClosureReason switch
            {
                OutageClosureReasons.Interrupted =>
                    $"Monitoring was interrupted. The outage that began at {started} UTC was last seen at {ending} UTC ({FormatDuration(duration)}).",
                OutageClosureReasons.MonitoringDisabled =>
                    $"Connection monitoring was turned off. The outage that began at {started} UTC was closed at {ending} UTC ({FormatDuration(duration)}).",
                _ => $"Connection restored after {FormatDuration(duration)} (down {started}\u2013{ending} UTC).",
            };
        }
        else
        {
            body = "Connection restored.";
        }

        return new NotificationMessage("Connection restored", body, "connection.restored", DateTime.UtcNow, null, null, null);
    }

    private static string FormatDuration(TimeSpan duration) =>
        duration.TotalHours >= 1
            ? $"{(int)duration.TotalHours}h {duration.Minutes}m {duration.Seconds}s"
            : $"{(int)duration.TotalMinutes}m {duration.Seconds}s";

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
