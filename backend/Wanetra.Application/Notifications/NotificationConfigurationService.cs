using System.Text.Json;
using System.Text.Json.Nodes;
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
            stored.TryGetValue(configuration.Provider, out var existing);
            if (existing is null
                && !configuration.Enabled
                && IsUnconfigured(configuration.Provider, configuration.ConfigurationJson))
            {
                continue;
            }
            string json;
            if (string.IsNullOrWhiteSpace(configuration.ConfigurationJson))
            {
                if (existing is null)
                {
                    continue;
                }

                json = existing.ConfigurationJson;
            }
            else
            {
                json = existing is null
                    ? configuration.ConfigurationJson
                    : MergeBlankFields(existing.ConfigurationJson, configuration.ConfigurationJson);
            }

            Validate(configuration.Provider, json);
            prepared.Add(new NotificationConfiguration
            {
                Id = existing?.Id ?? 0,
                Provider = configuration.Provider,
                Enabled = configuration.Enabled,
                ConfigurationJson = json,
                CreatedAt = existing?.CreatedAt ?? now,
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
    private static bool IsUnconfigured(string provider, string configurationJson)
    {
        if (string.IsNullOrWhiteSpace(configurationJson))
        {
            return true;
        }

        JsonNode? node;
        try
        {
            node = JsonNode.Parse(configurationJson);
        }
        catch (JsonException)
        {
            return false;
        }

        if (node is not JsonObject configuration)
        {
            return false;
        }

        return provider.ToLowerInvariant() switch
        {
            "ntfy" => IsBlank(configuration["serverUrl"]) && IsBlank(configuration["topic"]),
            "webhook" => IsBlank(configuration["url"]),
            _ => false,
        };
    }

    private static string MergeBlankFields(string storedJson, string updateJson)
    {
        JsonNode? storedNode;
        JsonNode? updateNode;
        try
        {
            storedNode = JsonNode.Parse(storedJson);
            updateNode = JsonNode.Parse(updateJson);
        }
        catch (JsonException)
        {
            throw new ArgumentException("Invalid notification configuration JSON.");
        }

        if (storedNode is not JsonObject stored || updateNode is not JsonObject update)
        {
            return updateJson;
        }

        foreach (var (key, storedValue) in stored)
        {
            if (!update.TryGetPropertyValue(key, out var updatedValue) || IsBlank(updatedValue))
            {
                update[key] = storedValue?.DeepClone();
            }
        }

        return update.ToJsonString();
    }

    private static bool IsBlank(JsonNode? value) =>
        value is null
        || value is JsonValue jsonValue
            && jsonValue.TryGetValue<string>(out var text)
            && string.IsNullOrWhiteSpace(text);
}
