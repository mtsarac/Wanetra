namespace Wanetra.Application.Settings;

public enum SettingSource
{
    Default,
    Stored,
    Environment,
}

public static class SettingSourceNames
{
    public const string Default = "default";
    public const string Stored = "stored";
    public const string Environment = "environment";
}

public static class SettingLockReasons
{
    public const string Environment = "environment";
    public const string Restricted = "restricted";
}

public sealed record EffectiveSetting(
    SettingDefinition Definition,
    object? Value,
    SettingSource Source,
    DateTime? UpdatedAt)
{
    public string Key => Definition.Key;
    public string Group => Definition.Group;
    public string Type => Definition.TypeString;
    public object? DefaultValue => Definition.DefaultValue;
    public bool Nullable => Definition.Nullable;

    public string SourceString => Source switch
    {
        SettingSource.Environment => SettingSourceNames.Environment,
        SettingSource.Stored => SettingSourceNames.Stored,
        _ => SettingSourceNames.Default,
    };

    public bool Locked => Definition.Restricted || Source == SettingSource.Environment;

    public string? LockReason =>
        Definition.Restricted ? SettingLockReasons.Restricted :
        Source == SettingSource.Environment ? SettingLockReasons.Environment :
        null;

    public string EnvironmentVariable => Definition.EnvironmentVariable;
    public IReadOnlyList<string>? Options => Definition.Options;
    public int? Min => Definition.Min;
    public int? Max => Definition.Max;
}
