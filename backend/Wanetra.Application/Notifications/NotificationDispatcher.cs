using Microsoft.Extensions.Logging;
using Wanetra.Domain;

namespace Wanetra.Application.Notifications;

public sealed class NotificationDispatcher(
    INotificationConfigurationRepository configurationRepository,
    IAlertStateRepository alertRepository,
    IEnumerable<INotificationProvider> providers,
    TimeProvider timeProvider,
    ILogger<NotificationDispatcher> logger)
{
    private static readonly SemaphoreSlim DispatchGate = new(1, 1);
    private const int MaxErrorSummaryLength = 128;

    public async Task DispatchPendingAsync(CancellationToken cancellationToken)
    {
        await DispatchGate.WaitAsync(cancellationToken);
        try
        {
            var pendingEvents = await alertRepository.GetPendingNotificationEventsAsync(cancellationToken);
            foreach (var degradationEvent in pendingEvents)
            {
                await DispatchEventAsync(degradationEvent, cancellationToken);
            }

            var pendingOutages = await alertRepository.GetPendingNotificationOutagesAsync(cancellationToken);
            foreach (var outage in pendingOutages)
            {
                await DispatchEventAsync(outage, cancellationToken);
            }

            var pendingDeviceAlerts = await alertRepository.GetPendingNotificationDeviceAlertsAsync(cancellationToken);
            foreach (var deviceAlert in pendingDeviceAlerts)
            {
                await DispatchEventAsync(deviceAlert, cancellationToken);
            }
        }
        finally
        {
            DispatchGate.Release();
        }
    }

    public async Task DispatchAsync(NotificationTrigger trigger, long degradationEventId, CancellationToken cancellationToken)
    {
        await DispatchGate.WaitAsync(cancellationToken);
        try
        {
            var degradationEvent = await alertRepository.GetEventAsync(degradationEventId, cancellationToken);
            if (degradationEvent is null)
            {
                logger.LogError("Notification skipped: degradation event {EventId} not found", degradationEventId);
                return;
            }

            await DispatchTriggerAsync(degradationEvent, trigger, cancellationToken);
        }
        finally
        {
            DispatchGate.Release();
        }
    }

    public async Task DispatchOutageAsync(NotificationTrigger trigger, long outageId, CancellationToken cancellationToken)
    {
        await DispatchGate.WaitAsync(cancellationToken);
        try
        {
            var outage = await alertRepository.GetOutageAsync(outageId, cancellationToken);
            if (outage is null)
            {
                logger.LogError("Notification skipped: outage {EventId} not found", outageId);
                return;
            }

            await DispatchTriggerAsync(outage, trigger, cancellationToken);
        }
        finally
        {
            DispatchGate.Release();
        }
    }

    public async Task DispatchDeviceAlertAsync(NotificationTrigger trigger, long alertId, CancellationToken cancellationToken)
    {
        await DispatchGate.WaitAsync(cancellationToken);
        try
        {
            var alert = await alertRepository.GetDeviceAlertAsync(alertId, cancellationToken);
            if (alert is null)
            {
                logger.LogError("Notification skipped: device alert {EventId} not found", alertId);
                return;
            }

            await DispatchTriggerAsync(alert, trigger, cancellationToken);
        }
        finally
        {
            DispatchGate.Release();
        }
    }

    public async Task SendTestAsync(string providerName, string configurationJson, CancellationToken cancellationToken)
    {
        var provider = providers.FirstOrDefault(candidate =>
            string.Equals(candidate.Name, providerName, StringComparison.OrdinalIgnoreCase));
        if (provider is null)
        {
            throw new ArgumentException($"Unknown provider: {providerName}", nameof(providerName));
        }

        await provider.SendAsync(NotificationMessageBuilder.Test(provider.Name), configurationJson, cancellationToken);
    }

    private async Task DispatchEventAsync(INotifiableEvent notifiable, CancellationToken cancellationToken)
    {
        await EnsureSnapshotAsync(notifiable, NotificationTrigger.Opened, cancellationToken);
        foreach (var delivery in Deliveries(notifiable, NotificationTrigger.Opened))
        {
            await DispatchDeliveryAsync(notifiable, delivery, cancellationToken);
        }

        if (notifiable.IsRecovered)
        {
            await EnsureSnapshotAsync(notifiable, NotificationTrigger.Recovered, cancellationToken);
            foreach (var delivery in Deliveries(notifiable, NotificationTrigger.Recovered))
            {
                await DispatchDeliveryAsync(notifiable, delivery, cancellationToken);
            }
        }
    }

    private async Task EnsureSnapshotAsync(
        INotifiableEvent notifiable,
        NotificationTrigger trigger,
        CancellationToken cancellationToken)
    {
        if (IsSnapshotInitialized(notifiable, trigger))
        {
            return;
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        if (trigger == NotificationTrigger.Opened)
        {
            var configurations = await configurationRepository.ListAsync(cancellationToken);
            foreach (var configuration in configurations.Where(configuration => configuration.Enabled).OrderBy(configuration => configuration.Id))
            {
                var alreadySent = notifiable.NotificationSent;
                AddDelivery(notifiable, new NotificationDelivery
                {
                    Trigger = NotificationTrigger.Opened,
                    ConfigurationId = configuration.Id,
                    Provider = configuration.Provider,
                    Status = alreadySent ? NotificationDeliveryStatus.Delivered : NotificationDeliveryStatus.Pending,
                    AttemptCount = alreadySent ? 1 : 0,
                    LastAttemptAt = alreadySent ? now : null,
                    DeliveredAt = alreadySent ? now : null,
                });
            }

            notifiable.OpenedDeliveryInitialized = true;
        }
        else
        {
            await EnsureSnapshotAsync(notifiable, NotificationTrigger.Opened, cancellationToken);
            foreach (var openedDelivery in Deliveries(notifiable, NotificationTrigger.Opened))
            {
                var alreadySent = notifiable.RecoveryNotificationSent;
                AddDelivery(notifiable, new NotificationDelivery
                {
                    Trigger = NotificationTrigger.Recovered,
                    ConfigurationId = openedDelivery.ConfigurationId,
                    Provider = openedDelivery.Provider,
                    Status = alreadySent ? NotificationDeliveryStatus.Delivered : NotificationDeliveryStatus.Pending,
                    AttemptCount = alreadySent ? 1 : 0,
                    LastAttemptAt = alreadySent ? now : null,
                    DeliveredAt = alreadySent ? now : null,
                });
            }

            notifiable.RecoveryDeliveryInitialized = true;
        }

        UpdateSentFlags(notifiable);
        await alertRepository.SaveChangesAsync(cancellationToken);
    }

    private async Task DispatchDeliveryAsync(
        INotifiableEvent notifiable,
        NotificationDelivery delivery,
        CancellationToken cancellationToken)
    {
        if (delivery.Status != NotificationDeliveryStatus.Pending)
        {
            return;
        }

        if (delivery.Trigger == NotificationTrigger.Recovered)
        {
            var openedDelivery = Deliveries(notifiable, NotificationTrigger.Opened)
                .SingleOrDefault(opened => opened.ConfigurationId == delivery.ConfigurationId);
            if (openedDelivery is null || openedDelivery.Status == NotificationDeliveryStatus.Skipped)
            {
                Skip(delivery, "opening notification was not delivered");
                UpdateSentFlags(notifiable);
                await alertRepository.SaveChangesAsync(cancellationToken);
                return;
            }

            if (openedDelivery.Status != NotificationDeliveryStatus.Delivered)
            {
                return;
            }
        }

        var configuration = (await configurationRepository.ListAsync(cancellationToken))
            .SingleOrDefault(candidate => candidate.Id == delivery.ConfigurationId);
        if (configuration is null || !configuration.Enabled)
        {
            Skip(delivery, "configuration disabled or removed");
            UpdateSentFlags(notifiable);
            await alertRepository.SaveChangesAsync(cancellationToken);
            return;
        }

        var provider = providers.FirstOrDefault(candidate =>
            string.Equals(candidate.Name, delivery.Provider, StringComparison.OrdinalIgnoreCase));
        if (provider is null)
        {
            Skip(delivery, "provider unavailable");
            UpdateSentFlags(notifiable);
            await alertRepository.SaveChangesAsync(cancellationToken);
            return;
        }

        delivery.AttemptCount++;
        delivery.LastAttemptAt = timeProvider.GetUtcNow().UtcDateTime;
        try
        {
            await provider.SendAsync(
                BuildMessage(delivery.Trigger, notifiable),
                configuration.ConfigurationJson,
                cancellationToken);
            delivery.Status = NotificationDeliveryStatus.Delivered;
            delivery.DeliveredAt = timeProvider.GetUtcNow().UtcDateTime;
            delivery.LastErrorSummary = null;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            delivery.LastErrorSummary = Summarize(exception);
            logger.LogWarning(
                "Notification via {Provider} failed for event {EventId} ({FailureType})",
                delivery.Provider,
                notifiable.Id,
                exception.GetType().Name);
        }

        UpdateSentFlags(notifiable);
        await alertRepository.SaveChangesAsync(cancellationToken);
    }

    private async Task DispatchTriggerAsync(
        INotifiableEvent notifiable,
        NotificationTrigger trigger,
        CancellationToken cancellationToken)
    {
        await EnsureSnapshotAsync(notifiable, trigger, cancellationToken);
        foreach (var delivery in Deliveries(notifiable, trigger))
        {
            await DispatchDeliveryAsync(notifiable, delivery, cancellationToken);
        }
    }

    private void AddDelivery(INotifiableEvent notifiable, NotificationDelivery delivery)
    {
        notifiable.Attach(delivery);
        alertRepository.AddNotificationDelivery(delivery);
    }

    private static NotificationMessage BuildMessage(NotificationTrigger trigger, INotifiableEvent notifiable) =>
        notifiable switch
        {
            DegradationEvent degradationEvent => NotificationMessageBuilder.Build(trigger, degradationEvent),
            Outage outage => NotificationMessageBuilder.Build(trigger, outage),
            DeviceAlert deviceAlert => NotificationMessageBuilder.Build(trigger, deviceAlert),
            _ => throw new ArgumentOutOfRangeException(nameof(notifiable), notifiable.GetType(), null),
        };

    private static IReadOnlyList<NotificationDelivery> Deliveries(INotifiableEvent notifiable, NotificationTrigger trigger) =>
        notifiable.Deliveries
            .Where(delivery => delivery.Trigger == trigger)
            .OrderBy(delivery => delivery.ConfigurationId)
            .ToList();

    private static bool IsSnapshotInitialized(INotifiableEvent notifiable, NotificationTrigger trigger) =>
        trigger == NotificationTrigger.Opened
            ? notifiable.OpenedDeliveryInitialized
            : notifiable.RecoveryDeliveryInitialized;

    private static void Skip(NotificationDelivery delivery, string reason)
    {
        delivery.Status = NotificationDeliveryStatus.Skipped;
        delivery.LastErrorSummary = reason;
    }

    private static string Summarize(Exception exception)
    {
        var summary = exception.GetType().Name;
        return summary.Length <= MaxErrorSummaryLength ? summary : summary[..MaxErrorSummaryLength];
    }

    private static void UpdateSentFlags(INotifiableEvent notifiable)
    {
        notifiable.NotificationSent = AllDelivered(Deliveries(notifiable, NotificationTrigger.Opened));
        notifiable.RecoveryNotificationSent = AllDelivered(Deliveries(notifiable, NotificationTrigger.Recovered));
    }

    private static bool AllDelivered(IReadOnlyList<NotificationDelivery> deliveries) =>
        deliveries.Count > 0 && deliveries.All(delivery => delivery.Status == NotificationDeliveryStatus.Delivered);
}
