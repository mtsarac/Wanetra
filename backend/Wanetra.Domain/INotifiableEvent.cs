namespace Wanetra.Domain;

// An event that opens and later recovers, and tells the configured destinations about both.
public interface INotifiableEvent
{
    long Id { get; }
    bool NotificationSent { get; set; }
    bool RecoveryNotificationSent { get; set; }
    bool OpenedDeliveryInitialized { get; set; }
    bool RecoveryDeliveryInitialized { get; set; }
    bool IsRecovered { get; }
    IList<NotificationDelivery> Deliveries { get; }
    void Attach(NotificationDelivery delivery);
}
