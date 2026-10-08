using System.Collections.Immutable;

namespace Wanetra.Application.Settings;

public sealed class SettingsSnapshot
{
    private volatile ImmutableDictionary<string, EffectiveSetting> settings =
        ImmutableDictionary<string, EffectiveSetting>.Empty;

    private readonly SemaphoreSlim writeLock = new(1, 1);

    public SemaphoreSlim WriteLock => writeLock;

    public bool IsInitialized => !settings.IsEmpty;

    public IReadOnlyList<EffectiveSetting> GetAll()
    {
        EnsureInitialized();
        return SettingDefinitions.All
            .Select(def => settings[def.Key])
            .ToList();
    }

    public EffectiveSetting Get(string key)
    {
        EnsureInitialized();
        if (settings.TryGetValue(key, out var setting))
        {
            return setting;
        }

        throw new KeyNotFoundException($"Setting with key '{key}' was not found.");
    }

    public string GetString(string key)
    {
        var value = Get(key).Value;
        return value is string s ? s : value?.ToString() ?? string.Empty;
    }

    public string? GetOptionalString(string key) =>
        Get(key).Value as string;

    public int GetInt(string key)
    {
        var value = Get(key).Value;
        return value is int i ? i : Convert.ToInt32(value);
    }

    public int? GetOptionalInt(string key)
    {
        var value = Get(key).Value;
        return value is null ? null : (value is int i ? i : Convert.ToInt32(value));
    }

    public bool GetBool(string key)
    {
        var value = Get(key).Value;
        return value is bool b ? b : Convert.ToBoolean(value);
    }

    public string SpeedTestEngine => GetString(SettingKeys.SpeedTestEngine);

    internal void Swap(IReadOnlyDictionary<string, EffectiveSetting> newSettings)
    {
        settings = newSettings.ToImmutableDictionary();
    }

    private void EnsureInitialized()
    {
        if (settings.IsEmpty)
        {
            throw new InvalidOperationException("Settings have not been initialized.");
        }
    }
}
