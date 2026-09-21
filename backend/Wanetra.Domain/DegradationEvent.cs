namespace Wanetra.Domain;

public class DegradationEvent
{
    public long Id { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public DegradationStatus Status { get; set; }
    public required string Reason { get; set; }

    public double? BaselineDownloadMbps { get; set; }
    public double? WorstDownloadMbps { get; set; }

    public double? BaselineUploadMbps { get; set; }
    public double? WorstUploadMbps { get; set; }

    public double? MaxLatencyMs { get; set; }
    public double? MaxJitterMs { get; set; }
    public double? MaxPacketLossPercent { get; set; }

    public int ConsecutiveUnhealthyMeasurements { get; set; }
    public int ConsecutiveHealthyMeasurements { get; set; }

    public bool NotificationSent { get; set; }
    public bool RecoveryNotificationSent { get; set; }
}
