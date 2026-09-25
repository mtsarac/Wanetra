namespace Wanetra.Domain;

public enum NotificationTrigger
{
    Opened,
    Recovered,
}

public enum NotificationDeliveryStatus
{
    Pending,
    Delivered,
    Skipped,
}

public sealed class NotificationDelivery
{
    public long Id { get; set; }
    public long DegradationEventId { get; set; }
    public DegradationEvent? DegradationEvent { get; set; }
    public NotificationTrigger Trigger { get; set; }
    public int ConfigurationId { get; set; }
    public required string Provider { get; set; }
    public NotificationDeliveryStatus Status { get; set; }
    public int AttemptCount { get; set; }
    public DateTime? LastAttemptAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public string? LastErrorSummary { get; set; }
}
