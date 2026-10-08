namespace Wanetra.Application.Settings;

public enum SettingsErrorKind
{
    InvalidSetting,
    UnknownSetting,
    SettingLocked,
}

public sealed class SettingsException(
    SettingsErrorKind kind,
    string key,
    string message) : Exception(message)
{
    public SettingsErrorKind Kind { get; } = kind;
    public string Key { get; } = key;
}
