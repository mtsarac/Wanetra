namespace Wanetra.Domain;

public enum DeviceAlertKind
{
    NewDevice,
    Offline,
}

public enum DeviceAlertStatus
{
    Active,
    Resolved,
}

public class DeviceAlert : INotifiableEvent
{
    public long Id { get; set; }
    public long DeviceId { get; set; }
    public Device? Device { get; set; }
    public DeviceAlertKind Kind { get; set; }
    public DeviceAlertStatus Status { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }

    // What the device looked like when the alert opened, so the message stays right after renames or forgetting.
    public required string DeviceLabel { get; set; }
    public required string Mac { get; set; }
    public string? Ip { get; set; }
    public string? Vendor { get; set; }

    public bool NotificationSent { get; set; }
    public bool RecoveryNotificationSent { get; set; }
    public bool OpenedDeliveryInitialized { get; set; }
    public bool RecoveryDeliveryInitialized { get; set; }
    public List<NotificationDelivery> NotificationDeliveries { get; set; } = [];

    // A new device is a one-off notice; only an offline alert has a matching "back online" message.
    public bool IsRecovered => Kind == DeviceAlertKind.Offline && Status == DeviceAlertStatus.Resolved;

    IList<NotificationDelivery> INotifiableEvent.Deliveries => NotificationDeliveries;

    void INotifiableEvent.Attach(NotificationDelivery delivery)
    {
        delivery.DeviceAlertId = Id;
        delivery.DegradationEventId = null;
        delivery.OutageId = null;
    }
}
