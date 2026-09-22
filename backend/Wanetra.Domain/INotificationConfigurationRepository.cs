namespace Wanetra.Domain;

public interface INotificationConfigurationRepository
{
    Task<IReadOnlyList<NotificationConfiguration>> ListAsync(CancellationToken cancellationToken);

    Task SaveAsync(IReadOnlyList<NotificationConfiguration> configurations, CancellationToken cancellationToken);
}
