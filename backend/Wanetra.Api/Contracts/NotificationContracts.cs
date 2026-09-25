using System.Text.Json;
using Wanetra.Domain;

namespace Wanetra.Api.Contracts;

public sealed record NotificationConfigurationResponse(
    int Id,
    string Provider,
    bool Enabled,
    bool HasConfiguration,
    DateTime UpdatedAt,
    string? ServerUrl,
    string? Topic,
    string? Priority,
    string? Tags,
    string? Method,
    bool HasUrl,
    bool HasHeaders,
    bool HasCredentials)
{
    public static NotificationConfigurationResponse From(NotificationConfiguration configuration)
    {
        var serverUrl = (string?)null;
        var topic = (string?)null;
        var priority = (string?)null;
        var tags = (string?)null;
        var method = (string?)null;
        var hasUrl = false;
        var hasHeaders = false;
        var hasCredentials = false;
        try
        {
            using var document = JsonDocument.Parse(configuration.ConfigurationJson);
            var root = document.RootElement;
            if (string.Equals(configuration.Provider, "ntfy", StringComparison.OrdinalIgnoreCase))
            {
                serverUrl = ReadString(root, "serverUrl");
                topic = ReadString(root, "topic");
                priority = ReadString(root, "priority");
                tags = ReadArray(root, "tags");
                hasCredentials = HasValue(root, "username") || HasValue(root, "password") || HasValue(root, "token");
            }
            else if (string.Equals(configuration.Provider, "webhook", StringComparison.OrdinalIgnoreCase))
            {
                hasUrl = HasValue(root, "url");
                method = ReadString(root, "method") ?? "POST";
                hasHeaders = Find(root, "headers") is { ValueKind: JsonValueKind.Object } headers
                    && headers.EnumerateObject().Any();
            }
        }
        catch (JsonException)
        {
        }

        return new NotificationConfigurationResponse(
            configuration.Id,
            configuration.Provider,
            configuration.Enabled,
            !string.IsNullOrWhiteSpace(configuration.ConfigurationJson) && configuration.ConfigurationJson != "{}",
            configuration.UpdatedAt,
            serverUrl,
            topic,
            priority,
            tags,
            method,
            hasUrl,
            hasHeaders,
            hasCredentials);
    }

    private static JsonElement? Find(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object
            ? element.EnumerateObject()
                .FirstOrDefault(property => string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                .Value
            : null;

    private static string? ReadString(JsonElement element, string name)
    {
        var value = Find(element, name);
        return value is { ValueKind: JsonValueKind.String } stringValue ? stringValue.GetString() : null;
    }

    private static string? ReadArray(JsonElement element, string name)
    {
        var value = Find(element, name);
        return value is { ValueKind: JsonValueKind.Array } array
            ? string.Join(", ", array.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.String).Select(item => item.GetString()))
            : null;
    }

    private static bool HasValue(JsonElement element, string name) =>
        !string.IsNullOrWhiteSpace(ReadString(element, name));
}

public sealed record NotificationConfigurationListResponse(
    IReadOnlyList<NotificationConfigurationResponse> Configurations)
{
    public static NotificationConfigurationListResponse From(IReadOnlyList<NotificationConfiguration> configurations) => new(
        configurations.Select(NotificationConfigurationResponse.From).ToList());
}

public sealed record NotificationConfigurationUpdateRequest(
    string Provider,
    bool Enabled,
    string ConfigurationJson);

public sealed record NotificationConfigurationUpdateListRequest(
    IReadOnlyList<NotificationConfigurationUpdateRequest> Configurations);

public sealed record NotificationTestRequest(
    int? Id,
    string? Provider,
    string? ConfigurationJson);
