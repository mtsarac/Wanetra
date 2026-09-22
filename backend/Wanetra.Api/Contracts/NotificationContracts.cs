using Wanetra.Domain;

namespace Wanetra.Api.Contracts;

public sealed record NotificationConfigurationResponse(
    int Id,
    string Provider,
    bool Enabled,
    bool HasConfiguration,
    DateTime UpdatedAt)
{
    public static NotificationConfigurationResponse From(NotificationConfiguration configuration) => new(
        configuration.Id,
        configuration.Provider,
        configuration.Enabled,
        !string.IsNullOrWhiteSpace(configuration.ConfigurationJson) && configuration.ConfigurationJson != "{}",
        configuration.UpdatedAt);
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
