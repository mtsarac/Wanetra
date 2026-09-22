using Wanetra.Domain;
namespace Wanetra.Application.Notifications;

public sealed class NotificationConfigurationService(
    INotificationConfigurationRepository repository,
    TimeProvider timeProvider)
{
    public Task<IReadOnlyList<NotificationConfiguration>> ListAsync(CancellationToken cancellationToken) =>
        repository.ListAsync(cancellationToken);

    public async Task<IReadOnlyList<NotificationConfiguration>> SaveAsync(
        IReadOnlyList<(string Provider, bool Enabled, string ConfigurationJson)> configurations,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var stored = (await repository.ListAsync(cancellationToken))
            .ToDictionary(configuration => configuration.Provider, StringComparer.OrdinalIgnoreCase);
        var prepared = new List<NotificationConfiguration>();
        foreach (var configuration in configurations)
        {
            // Blank JSON keeps stored secrets (frontend never sees them); blank for a new
            // provider means "not configured" and is skipped instead of failing validation.
            string json;
            if (string.IsNullOrWhiteSpace(configuration.ConfigurationJson))
            {
                if (!stored.TryGetValue(configuration.Provider, out var existing))
                {
                    continue;
                }

                json = existing.ConfigurationJson;
            }
            else
            {
                json = configuration.ConfigurationJson;
            }

            Validate(configuration.Provider, json);
            prepared.Add(new NotificationConfiguration
            {
                Provider = configuration.Provider,
                Enabled = configuration.Enabled,
                ConfigurationJson = json,
                CreatedAt = now,
                UpdatedAt = now,
            });
        }

        await repository.SaveAsync(prepared, cancellationToken);
        return prepared;
    }

    public static void Validate(string provider, string configurationJson)
    {
        if (string.Equals(provider, "ntfy", StringComparison.OrdinalIgnoreCase))
        {
            NtfyConfiguration.Parse(configurationJson);
        }
        else if (string.Equals(provider, "webhook", StringComparison.OrdinalIgnoreCase))
        {
            WebhookConfiguration.Parse(configurationJson);
        }
        else
        {
            throw new ArgumentException($"Unknown provider: {provider}", nameof(provider));
        }
    }
}
