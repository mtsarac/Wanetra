using Microsoft.Extensions.Logging;
using Wanetra.Domain;

namespace Wanetra.Application.Notifications;

public sealed class NotificationDispatcher(
    INotificationConfigurationRepository configurationRepository,
    IAlertStateRepository alertRepository,
    IEnumerable<INotificationProvider> providers,
    ILogger<NotificationDispatcher> logger)
{
    public async Task DispatchAsync(NotificationTrigger trigger, long degradationEventId, CancellationToken cancellationToken)
    {
        var configurations = (await configurationRepository.ListAsync(cancellationToken))
            .Where(configuration => configuration.Enabled)
            .OrderBy(configuration => configuration.Id)
            .ToList();
        if (configurations.Count == 0)
        {
            return;
        }

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

        var message = NotificationMessageBuilder.Build(trigger, degradationEvent);
        var providersByName = providers.ToDictionary(provider => provider.Name, StringComparer.OrdinalIgnoreCase);
        var attempted = false;

        foreach (var configuration in configurations)
        {
            if (!providersByName.TryGetValue(configuration.Provider, out var provider))
            {
                logger.LogWarning("Unknown notification provider {Provider} (id {Id}); skipping", configuration.Provider, configuration.Id);
                continue;
            }

            attempted = true;
            try
            {
                await provider.SendAsync(message, configuration.ConfigurationJson, cancellationToken);
            }
            catch (Exception ex)
            {
                // Preserve the degradation event; one bad provider must not crash the run or block others.
                logger.LogError(ex, "Notification via {Provider} failed for event {EventId}", configuration.Provider, degradationEventId);
            }
        }

        if (attempted)
        {
            MarkSent(trigger, degradationEvent);
            await alertRepository.SaveChangesAsync(cancellationToken);
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
