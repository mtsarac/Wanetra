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

            await EnsureSnapshotAsync(degradationEvent, trigger, cancellationToken);
            if (trigger == NotificationTrigger.Opened)
            {
                foreach (var delivery in Deliveries(degradationEvent, NotificationTrigger.Opened))
                {
                    await DispatchDeliveryAsync(degradationEvent, delivery, cancellationToken);
                }
            }
            else
            {
                foreach (var delivery in Deliveries(degradationEvent, NotificationTrigger.Recovered))
                {
                    await DispatchDeliveryAsync(degradationEvent, delivery, cancellationToken);
                }
            }
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

    private async Task DispatchEventAsync(DegradationEvent degradationEvent, CancellationToken cancellationToken)
    {
        await EnsureSnapshotAsync(degradationEvent, NotificationTrigger.Opened, cancellationToken);
        foreach (var delivery in Deliveries(degradationEvent, NotificationTrigger.Opened))
        {
            await DispatchDeliveryAsync(degradationEvent, delivery, cancellationToken);
        }

        if (degradationEvent.Status == DegradationStatus.Recovered)
        {
            await EnsureSnapshotAsync(degradationEvent, NotificationTrigger.Recovered, cancellationToken);
            foreach (var delivery in Deliveries(degradationEvent, NotificationTrigger.Recovered))
            {
                await DispatchDeliveryAsync(degradationEvent, delivery, cancellationToken);
            }
        }
    }

    private async Task EnsureSnapshotAsync(
        DegradationEvent degradationEvent,
        NotificationTrigger trigger,
        CancellationToken cancellationToken)
    {
        if (IsSnapshotInitialized(degradationEvent, trigger))
        {
            return;
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        if (trigger == NotificationTrigger.Opened)
        {
            var configurations = await configurationRepository.ListAsync(cancellationToken);
            foreach (var configuration in configurations.Where(configuration => configuration.Enabled).OrderBy(configuration => configuration.Id))
            {
                var alreadySent = degradationEvent.NotificationSent;
                alertRepository.AddNotificationDelivery(new NotificationDelivery
                {
                    DegradationEventId = degradationEvent.Id,
                    Trigger = NotificationTrigger.Opened,
                    ConfigurationId = configuration.Id,
                    Provider = configuration.Provider,
                    Status = alreadySent ? NotificationDeliveryStatus.Delivered : NotificationDeliveryStatus.Pending,
                    AttemptCount = alreadySent ? 1 : 0,
                    LastAttemptAt = alreadySent ? now : null,
                    DeliveredAt = alreadySent ? now : null,
                });
            }

            degradationEvent.OpenedDeliveryInitialized = true;
        }
        else
        {
            await EnsureSnapshotAsync(degradationEvent, NotificationTrigger.Opened, cancellationToken);
            foreach (var openedDelivery in Deliveries(degradationEvent, NotificationTrigger.Opened))
            {
                var alreadySent = degradationEvent.RecoveryNotificationSent;
                alertRepository.AddNotificationDelivery(new NotificationDelivery
                {
                    DegradationEventId = degradationEvent.Id,
                    Trigger = NotificationTrigger.Recovered,
                    ConfigurationId = openedDelivery.ConfigurationId,
                    Provider = openedDelivery.Provider,
                    Status = alreadySent ? NotificationDeliveryStatus.Delivered : NotificationDeliveryStatus.Pending,
                    AttemptCount = alreadySent ? 1 : 0,
                    LastAttemptAt = alreadySent ? now : null,
                    DeliveredAt = alreadySent ? now : null,
                });
            }

            degradationEvent.RecoveryDeliveryInitialized = true;
        }

        UpdateSentFlags(degradationEvent);
        await alertRepository.SaveChangesAsync(cancellationToken);
    }

    private async Task DispatchDeliveryAsync(
        DegradationEvent degradationEvent,
        NotificationDelivery delivery,
        CancellationToken cancellationToken)
    {
        if (delivery.Status != NotificationDeliveryStatus.Pending)
        {
            return;
        }

        if (delivery.Trigger == NotificationTrigger.Recovered)
        {
            var openedDelivery = Deliveries(degradationEvent, NotificationTrigger.Opened)
                .SingleOrDefault(opened => opened.ConfigurationId == delivery.ConfigurationId);
            if (openedDelivery is null || openedDelivery.Status == NotificationDeliveryStatus.Skipped)
            {
                Skip(delivery, "opening notification was not delivered");
                UpdateSentFlags(degradationEvent);
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
            UpdateSentFlags(degradationEvent);
            await alertRepository.SaveChangesAsync(cancellationToken);
            return;
        }

        var provider = providers.FirstOrDefault(candidate =>
            string.Equals(candidate.Name, delivery.Provider, StringComparison.OrdinalIgnoreCase));
        if (provider is null)
        {
            Skip(delivery, "provider unavailable");
            UpdateSentFlags(degradationEvent);
            await alertRepository.SaveChangesAsync(cancellationToken);
            return;
        }

        delivery.AttemptCount++;
        delivery.LastAttemptAt = timeProvider.GetUtcNow().UtcDateTime;
        try
        {
            await provider.SendAsync(
                NotificationMessageBuilder.Build(delivery.Trigger, degradationEvent),
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
                degradationEvent.Id,
                exception.GetType().Name);
        }

        UpdateSentFlags(degradationEvent);
        await alertRepository.SaveChangesAsync(cancellationToken);
    }

    private static IReadOnlyList<NotificationDelivery> Deliveries(DegradationEvent degradationEvent, NotificationTrigger trigger) =>
        degradationEvent.NotificationDeliveries
            .Where(delivery => delivery.Trigger == trigger)
            .OrderBy(delivery => delivery.ConfigurationId)
            .ToList();

    private static bool IsSnapshotInitialized(DegradationEvent degradationEvent, NotificationTrigger trigger) =>
        trigger == NotificationTrigger.Opened
            ? degradationEvent.OpenedDeliveryInitialized
            : degradationEvent.RecoveryDeliveryInitialized;

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

    private static void UpdateSentFlags(DegradationEvent degradationEvent)
    {
        degradationEvent.NotificationSent = AllDelivered(Deliveries(degradationEvent, NotificationTrigger.Opened));
        degradationEvent.RecoveryNotificationSent = AllDelivered(Deliveries(degradationEvent, NotificationTrigger.Recovered));
    }

    private static bool AllDelivered(IReadOnlyList<NotificationDelivery> deliveries) =>
        deliveries.Count > 0 && deliveries.All(delivery => delivery.Status == NotificationDeliveryStatus.Delivered);
}
