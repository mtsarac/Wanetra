namespace Wanetra.Domain;

public enum OutageScope
{
    LocalNetwork,
    Wan,
    Dns,
}

public enum OutageStatus
{
    Active,
    Recovered,
}

public static class OutageClosureReasons
{
    public const string Interrupted = "interrupted";
    public const string MonitoringDisabled = "monitoring disabled";
}

public class Outage : INotifiableEvent
{
    public long Id { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public OutageStatus Status { get; set; }
    public OutageScope Scope { get; set; }
    public required string Reason { get; set; }
    public string? ClosureReason { get; set; }
    public DateTime LastObservedAt { get; set; }

    public bool NotificationSent { get; set; }
    public bool RecoveryNotificationSent { get; set; }
    public bool OpenedDeliveryInitialized { get; set; }
    public bool RecoveryDeliveryInitialized { get; set; }
    public List<NotificationDelivery> NotificationDeliveries { get; set; } = [];

    public bool IsRecovered => Status == OutageStatus.Recovered;

    IList<NotificationDelivery> INotifiableEvent.Deliveries => NotificationDeliveries;

    void INotifiableEvent.Attach(NotificationDelivery delivery)
    {
        delivery.OutageId = Id;
        delivery.DegradationEventId = null;
        delivery.DeviceAlertId = null;
    }
}
