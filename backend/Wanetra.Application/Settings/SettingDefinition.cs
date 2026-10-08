namespace Wanetra.Application.Settings;

public enum SettingType
{
    Choice,
    Integer,
    Boolean,
    Text,
}

public static class SettingGroupNames
{
    public const string SpeedTest = "speedtest";
    public const string Retention = "retention";
}

public static class SettingTypeNames
{
    public const string Choice = "choice";
    public const string Integer = "integer";
    public const string Boolean = "boolean";
    public const string Text = "text";
}

public sealed record SettingDefinition(
    string Key,
    string Group,
    SettingType Type,
    object? DefaultValue,
    bool Nullable,
    string ConfigurationKey,
    bool Restricted = false,
    IReadOnlyList<string>? Options = null,
    int? Min = null,
    int? Max = null)
{
    public string EnvironmentVariable => ConfigurationKey.Replace(":", "__");

    public string TypeString => Type switch
    {
        SettingType.Choice => SettingTypeNames.Choice,
        SettingType.Integer => SettingTypeNames.Integer,
        SettingType.Boolean => SettingTypeNames.Boolean,
        SettingType.Text => SettingTypeNames.Text,
        _ => "text",
    };
}
