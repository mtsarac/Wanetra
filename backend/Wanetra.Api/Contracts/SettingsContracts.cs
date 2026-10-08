namespace Wanetra.Api.Contracts;

public sealed record SettingResponse(
    string Key,
    string Group,
    string Type,
    object? Value,
    object? DefaultValue,
    bool Nullable,
    string Source,
    bool Locked,
    string? LockReason,
    string EnvironmentVariable,
    IReadOnlyList<string>? Options,
    int? Min,
    int? Max,
    string? UpdatedAt);

public sealed record SettingsListResponse(IReadOnlyList<SettingResponse> Settings);

public sealed record UpdateSettingsRequest(Dictionary<string, System.Text.Json.JsonElement>? Values);
