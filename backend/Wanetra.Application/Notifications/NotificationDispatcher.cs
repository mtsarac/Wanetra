using Microsoft.Extensions.Logging;
using Wanetra.Domain;

namespace Wanetra.Application.Notifications;

public sealed class NotificationDispatcher(
    INotificationConfigurationRepository configurationRepository,
    IAlertStateRepository alertRepository,
    IEnumerable<INotificationProvider> providers,
    ILogger<NotificationDispatcher> logger)
{
    private static readonly SemaphoreSlim DispatchGate = new(1, 1);

    public async Task DispatchPendingAsync(CancellationToken cancellationToken)
    {
        var pendingEvents = await alertRepository.GetPendingNotificationEventsAsync(cancellationToken);
        foreach (var degradationEvent in pendingEvents)
        {
            if (!degradationEvent.NotificationSent)
            {
                await DispatchAsync(NotificationTrigger.Opened, degradationEvent.Id, cancellationToken);
            }

            if (degradationEvent.Status == DegradationStatus.Recovered
                && degradationEvent.NotificationSent
                && !degradationEvent.RecoveryNotificationSent)
            {
                await DispatchAsync(NotificationTrigger.Recovered, degradationEvent.Id, cancellationToken);
            }
        }
    }

    public async Task DispatchAsync(NotificationTrigger trigger, long degradationEventId, CancellationToken cancellationToken)
    {
        await DispatchGate.WaitAsync(cancellationToken);
        try
        {
            await DispatchCoreAsync(trigger, degradationEventId, cancellationToken);
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

    private async Task DispatchCoreAsync(
        NotificationTrigger trigger,
        long degradationEventId,
        CancellationToken cancellationToken)
    {
        var degradationEvent = await alertRepository.GetEventAsync(degradationEventId, cancellationToken);
        if (degradationEvent is null)
        {
            logger.LogError("Notification skipped: degradation event {EventId} not found", degradationEventId);
            return;
        }

        if (AlreadySent(trigger, degradationEvent))
        {
            return;
        }

        var configurations = (await configurationRepository.ListAsync(cancellationToken))
            .Where(configuration => configuration.Enabled)
            .OrderBy(configuration => configuration.Id)
            .ToList();
        if (configurations.Count == 0)
        {
            return;
        }

        var message = NotificationMessageBuilder.Build(trigger, degradationEvent);
        var providersByName = providers.ToDictionary(provider => provider.Name, StringComparer.OrdinalIgnoreCase);
        var delivered = false;

        foreach (var configuration in configurations)
        {
            if (!providersByName.TryGetValue(configuration.Provider, out var provider))
            {
                logger.LogWarning("Unknown notification provider {Provider} (id {Id}); skipping", configuration.Provider, configuration.Id);
                continue;
            }

            try
            {
                await provider.SendAsync(message, configuration.ConfigurationJson, cancellationToken);
                delivered = true;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogWarning(
                    "Notification via {Provider} failed for event {EventId} ({FailureType})",
                    configuration.Provider,
                    degradationEventId,
                    exception.GetType().Name);
            }
        }

        if (delivered)
        {
            MarkSent(trigger, degradationEvent);
            await alertRepository.SaveChangesAsync(cancellationToken);
        }
    }

    private static bool AlreadySent(NotificationTrigger trigger, DegradationEvent degradationEvent) =>
        trigger == NotificationTrigger.Opened ? degradationEvent.NotificationSent : degradationEvent.RecoveryNotificationSent;

    private static void MarkSent(NotificationTrigger trigger, DegradationEvent degradationEvent)
    {
        if (trigger == NotificationTrigger.Opened)
        {
            degradationEvent.NotificationSent = true;
        }
        else
        {
            degradationEvent.RecoveryNotificationSent = true;
        }
    }
}
