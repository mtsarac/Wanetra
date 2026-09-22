namespace Wanetra.Application.Notifications;

public interface INotificationProvider
{
    string Name { get; }

    Task SendAsync(NotificationMessage message, string configurationJson, CancellationToken cancellationToken);
}
