using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Wanetra.Domain;

namespace Wanetra.Application.Settings;

public sealed class SettingsService(
    IAppSettingRepository repository,
    IConfiguration configuration,
    SettingsSnapshot snapshot,
    TimeProvider timeProvider)
{
    public IReadOnlyList<EffectiveSetting> GetAll() => snapshot.GetAll();

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        var stored = await repository.ListAsync(cancellationToken);
        var storedMap = stored.ToDictionary(s => s.Key, s => (s.Value, (DateTime?)s.UpdatedAt));
        var effective = ResolveEffectiveSettings(storedMap, configuration, changedKeys: null);
        snapshot.Swap(effective);
    }

    public async Task<IReadOnlyList<EffectiveSetting>> UpdateAsync(
        IReadOnlyDictionary<string, JsonElement> updates,
        CancellationToken cancellationToken)
    {
        await snapshot.WriteLock.WaitAsync(cancellationToken);
        try
        {
            var stored = await repository.ListAsync(cancellationToken);
            var storedMap = stored.ToDictionary(s => s.Key, s => (s.Value, (DateTime?)s.UpdatedAt));

            // 1. Unknown settings check
            foreach (var key in updates.Keys)
            {
                if (SettingDefinitions.Find(key) is null)
                {
                    throw new SettingsException(
                        SettingsErrorKind.UnknownSetting,
                        key,
                        $"Unknown setting '{key}'.");
                }
            }

            // 2. Locked and restricted check, and per-setting type parsing/validation
            var currentEffective = snapshot.GetAll().ToDictionary(s => s.Key);
            var parsedChanges = new Dictionary<string, string?>(StringComparer.Ordinal);
            var parsedCandidateValues = new Dictionary<string, object?>(StringComparer.Ordinal);

            foreach (var def in SettingDefinitions.All)
            {
                if (!updates.TryGetValue(def.Key, out var jsonElement))
                {
                    continue;
                }

                var current = currentEffective[def.Key];
                if (current.Locked)
                {
                    if (def.Restricted)
                    {
                        throw new SettingsException(
                            SettingsErrorKind.SettingLocked,
                            def.Key,
                            $"Setting '{def.Key}' is restricted and can only be set via the environment variable '{def.EnvironmentVariable}'.");
                    }

                    throw new SettingsException(
                        SettingsErrorKind.SettingLocked,
                        def.Key,
                        $"Setting '{def.Key}' is locked by the environment variable '{def.EnvironmentVariable}'.");
                }

                // Parse value
                var parsedValue = ParseJsonValue(def, jsonElement);
                parsedCandidateValues[def.Key] = parsedValue;
                parsedChanges[def.Key] = SerializeValue(def, parsedValue);
            }

            // 3. Build candidate stored map and validate candidate effective state (including cross-key)
            var now = timeProvider.GetUtcNow().UtcDateTime;
            var candidateStoredMap = new Dictionary<string, (string? Value, DateTime? UpdatedAt)>(storedMap, StringComparer.Ordinal);
            foreach (var (key, strVal) in parsedChanges)
            {
                if (strVal is null)
                {
                    candidateStoredMap.Remove(key);
                }
                else
                {
                    candidateStoredMap[key] = (strVal, now);
                }
            }

            var changedKeysSet = updates.Keys.ToHashSet(StringComparer.Ordinal);
            var resolvedNewEffective = ResolveEffectiveSettings(candidateStoredMap, configuration, changedKeysSet);

            // 4. Persist atomically
            await repository.SaveAsync(parsedChanges, now, cancellationToken);

            // 5. Swap snapshot
            snapshot.Swap(resolvedNewEffective);

            return snapshot.GetAll();
        }
        finally
        {
            snapshot.WriteLock.Release();
        }
    }

    public static Dictionary<string, EffectiveSetting> ResolveEffectiveSettings(
        IReadOnlyDictionary<string, (string? Value, DateTime? UpdatedAt)> storedMap,
        IConfiguration config,
        HashSet<string>? changedKeys)
    {
        var result = new Dictionary<string, EffectiveSetting>(StringComparer.Ordinal);

        foreach (var def in SettingDefinitions.All)
        {
            var envVal = config[def.ConfigurationKey];
            var hasEnv = !string.IsNullOrWhiteSpace(envVal);

            if (hasEnv)
            {
                var parsed = ParseConfigString(def, envVal!.Trim(), isEnv: true);
                result[def.Key] = new EffectiveSetting(
                    def,
                    parsed,
                    SettingSource.Environment,
                    UpdatedAt: null);
            }
            else if (!def.Restricted && storedMap.TryGetValue(def.Key, out var stored) && stored.Value is not null)
            {
                var parsed = ParseConfigString(def, stored.Value, isEnv: false);
                result[def.Key] = new EffectiveSetting(
                    def,
                    parsed,
                    SettingSource.Stored,
                    stored.UpdatedAt);
            }
            else
            {
                result[def.Key] = new EffectiveSetting(
                    def,
                    def.DefaultValue,
                    SettingSource.Default,
                    UpdatedAt: null);
            }
        }

        // Cross-key validation
        var effectiveEngine = (string)result[SettingKeys.SpeedTestEngine].Value!;
        var effectiveAcceptLicense = (bool)result[SettingKeys.SpeedTestOoklaAcceptLicense].Value!;

        if (string.Equals(effectiveEngine, SpeedTestEngineNames.Ookla, StringComparison.OrdinalIgnoreCase))
        {
            if (!effectiveAcceptLicense)
            {
                if (changedKeys is null)
                {
                    // Startup validation
                    var engineEnv = SettingDefinitions.Find(SettingKeys.SpeedTestEngine)!.EnvironmentVariable;
                    var licenseEnv = SettingDefinitions.Find(SettingKeys.SpeedTestOoklaAcceptLicense)!.EnvironmentVariable;
                    throw new InvalidOperationException(
                        $"The Ookla speed test engine requires license acceptance. Set {licenseEnv}=true to accept the Ookla EULA, or change {engineEnv} to librespeed or cloudflare.");
                }

                if (changedKeys.Contains(SettingKeys.SpeedTestOoklaAcceptLicense))
                {
                    throw new SettingsException(
                        SettingsErrorKind.InvalidSetting,
                        SettingKeys.SpeedTestOoklaAcceptLicense,
                        $"{SettingKeys.SpeedTestOoklaAcceptLicense}: the Ookla license cannot be rejected while {SettingKeys.SpeedTestEngine} is ookla.");
                }

                throw new SettingsException(
                    SettingsErrorKind.InvalidSetting,
                    SettingKeys.SpeedTestEngine,
                    $"{SettingKeys.SpeedTestEngine}: the Ookla engine requires {SettingKeys.SpeedTestOoklaAcceptLicense} to be true. Accept the Ookla license first.");
            }
        }

        return result;
    }

    private static object? ParseJsonValue(SettingDefinition def, JsonElement element)
    {
        if (element.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null; // Signals reset to default or null
        }

        switch (def.Type)
        {
            case SettingType.Choice:
                if (element.ValueKind != JsonValueKind.String)
                {
                    throw new SettingsException(
                        SettingsErrorKind.InvalidSetting,
                        def.Key,
                        $"Setting '{def.Key}' must be a string choice.");
                }
                var choice = element.GetString()?.Trim().ToLowerInvariant();
                if (choice is null || def.Options is null || !def.Options.Contains(choice))
                {
                    var validChoices = def.Options is null ? string.Empty : string.Join(", ", def.Options);
                    throw new SettingsException(
                        SettingsErrorKind.InvalidSetting,
                        def.Key,
                        $"Setting '{def.Key}' must be one of: {validChoices}.");
                }
                return choice;

            case SettingType.Integer:
                if (element.ValueKind != JsonValueKind.Number || !element.TryGetInt32(out var intVal))
                {
                    throw new SettingsException(
                        SettingsErrorKind.InvalidSetting,
                        def.Key,
                        $"Setting '{def.Key}' must be a valid integer.");
                }
                ValidateIntegerBounds(def, intVal);
                return intVal;

            case SettingType.Boolean:
                if (element.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                {
                    throw new SettingsException(
                        SettingsErrorKind.InvalidSetting,
                        def.Key,
                        $"Setting '{def.Key}' must be a boolean.");
                }
                return element.GetBoolean();

            case SettingType.Text:
                if (element.ValueKind != JsonValueKind.String)
                {
                    throw new SettingsException(
                        SettingsErrorKind.InvalidSetting,
                        def.Key,
                        $"Setting '{def.Key}' must be a string.");
                }
                var text = element.GetString()?.Trim();
                if (string.IsNullOrWhiteSpace(text))
                {
                    if (!def.Nullable)
                    {
                        throw new SettingsException(
                            SettingsErrorKind.InvalidSetting,
                            def.Key,
                            $"Setting '{def.Key}' cannot be empty.");
                    }
                    return null;
                }
                return text;

            default:
                throw new SettingsException(
                    SettingsErrorKind.InvalidSetting,
                    def.Key,
                    $"Unsupported type for setting '{def.Key}'.");
        }
    }

    private static object? ParseConfigString(SettingDefinition def, string rawValue, bool isEnv)
    {
        switch (def.Type)
        {
            case SettingType.Choice:
                var choice = rawValue.Trim().ToLowerInvariant();
                if (def.Options is null || !def.Options.Contains(choice))
                {
                    var message = isEnv
                        ? $"Environment configuration '{def.ConfigurationKey}' ({def.EnvironmentVariable}) must be one of: {string.Join(", ", def.Options ?? [])} (got '{rawValue}')."
                        : $"Stored setting '{def.Key}' must be one of: {string.Join(", ", def.Options ?? [])} (got '{rawValue}').";
                    throw new InvalidOperationException(message);
                }
                return choice;

            case SettingType.Integer:
                if (!int.TryParse(rawValue.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var intVal))
                {
                    var message = isEnv
                        ? $"Environment configuration '{def.ConfigurationKey}' ({def.EnvironmentVariable}) must be a valid integer (got '{rawValue}')."
                        : $"Stored setting '{def.Key}' must be a valid integer (got '{rawValue}').";
                    throw new InvalidOperationException(message);
                }
                if (def.Min.HasValue && intVal < def.Min.Value)
                {
                    var message = isEnv
                        ? $"Environment configuration '{def.ConfigurationKey}' ({def.EnvironmentVariable}) must be >= {def.Min.Value} (got '{intVal}')."
                        : $"Stored setting '{def.Key}' must be >= {def.Min.Value} (got '{intVal}').";
                    throw new InvalidOperationException(message);
                }
                if (def.Max.HasValue && intVal > def.Max.Value)
                {
                    var message = isEnv
                        ? $"Environment configuration '{def.ConfigurationKey}' ({def.EnvironmentVariable}) must be <= {def.Max.Value} (got '{intVal}')."
                        : $"Stored setting '{def.Key}' must be <= {def.Max.Value} (got '{intVal}').";
                    throw new InvalidOperationException(message);
                }
                return intVal;

            case SettingType.Boolean:
                if (!bool.TryParse(rawValue.Trim(), out var boolVal))
                {
                    var message = isEnv
                        ? $"Environment configuration '{def.ConfigurationKey}' ({def.EnvironmentVariable}) must be a boolean (got '{rawValue}')."
                        : $"Stored setting '{def.Key}' must be a boolean (got '{rawValue}').";
                    throw new InvalidOperationException(message);
                }
                return boolVal;

            case SettingType.Text:
                var text = rawValue.Trim();
                if (string.IsNullOrWhiteSpace(text))
                {
                    if (!def.Nullable)
                    {
                        var message = isEnv
                            ? $"Environment configuration '{def.ConfigurationKey}' ({def.EnvironmentVariable}) cannot be empty."
                            : $"Stored setting '{def.Key}' cannot be empty.";
                        throw new InvalidOperationException(message);
                    }
                    return null;
                }
                return text;

            default:
                return rawValue;
        }
    }

    private static void ValidateIntegerBounds(SettingDefinition def, int val)
    {
        if (def.Min.HasValue && val < def.Min.Value)
        {
            throw new SettingsException(
                SettingsErrorKind.InvalidSetting,
                def.Key,
                $"Setting '{def.Key}' must be at least {def.Min.Value}.");
        }

        if (def.Max.HasValue && val > def.Max.Value)
        {
            throw new SettingsException(
                SettingsErrorKind.InvalidSetting,
                def.Key,
                $"Setting '{def.Key}' must be at most {def.Max.Value}.");
        }
    }

    private static string? SerializeValue(SettingDefinition def, object? value)
    {
        if (value is null)
        {
            return null;
        }

        return def.Type switch
        {
            SettingType.Boolean => (bool)value ? "true" : "false",
            SettingType.Integer => ((int)value).ToString(CultureInfo.InvariantCulture),
            SettingType.Choice => (string)value,
            SettingType.Text => (string)value,
            _ => value.ToString(),
        };
    }
}
