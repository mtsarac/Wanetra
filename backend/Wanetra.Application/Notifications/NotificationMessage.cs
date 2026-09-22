namespace Wanetra.Application.Notifications;

public enum NotificationTrigger
{
    Opened,
    Recovered,
}

public sealed record NotificationMessage(
    string Title,
    string Body,
    string Event,
    DateTime Timestamp,
    double? DownloadMbps,
    double? BaselineDownloadMbps,
    double? DegradationPercent);
