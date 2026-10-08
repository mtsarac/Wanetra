namespace Wanetra.Application.Settings;

public static class SettingKeys
{
    public const string SpeedTestEngine = "speedtest.engine";
    public const string SpeedTestLibreSpeedTimeoutSeconds = "speedtest.librespeed.timeoutSeconds";
    public const string SpeedTestLibreSpeedServerId = "speedtest.librespeed.serverId";
    public const string SpeedTestLibreSpeedExecutablePath = "speedtest.librespeed.executablePath";
    public const string SpeedTestCloudflareTimeoutSeconds = "speedtest.cloudflare.timeoutSeconds";
    public const string SpeedTestCloudflareExecutablePath = "speedtest.cloudflare.executablePath";
    public const string SpeedTestOoklaTimeoutSeconds = "speedtest.ookla.timeoutSeconds";
    public const string SpeedTestOoklaServerId = "speedtest.ookla.serverId";
    public const string SpeedTestOoklaExecutablePath = "speedtest.ookla.executablePath";
    public const string SpeedTestOoklaAcceptLicense = "speedtest.ookla.acceptLicense";
    public const string RetentionDays = "retention.days";
}

public static class SpeedTestEngineNames
{
    public const string LibreSpeed = "librespeed";
    public const string Cloudflare = "cloudflare";
    public const string Ookla = "ookla";
}

public static class SettingDefinitions
{
    public static readonly IReadOnlyList<string> EngineChoices =
    [
        SpeedTestEngineNames.LibreSpeed,
        SpeedTestEngineNames.Cloudflare,
        SpeedTestEngineNames.Ookla,
    ];

    public static readonly IReadOnlyList<SettingDefinition> All =
    [
        new(
            Key: SettingKeys.SpeedTestEngine,
            Group: SettingGroupNames.SpeedTest,
            Type: SettingType.Choice,
            DefaultValue: SpeedTestEngineNames.LibreSpeed,
            Nullable: false,
            ConfigurationKey: "SpeedTest:Engine",
            Restricted: false,
            Options: EngineChoices),

        new(
            Key: SettingKeys.SpeedTestLibreSpeedTimeoutSeconds,
            Group: SettingGroupNames.SpeedTest,
            Type: SettingType.Integer,
            DefaultValue: 120,
            Nullable: false,
            ConfigurationKey: "SpeedTest:LibreSpeed:TimeoutSeconds",
            Restricted: false,
            Min: 10,
            Max: 1800),

        new(
            Key: SettingKeys.SpeedTestLibreSpeedServerId,
            Group: SettingGroupNames.SpeedTest,
            Type: SettingType.Integer,
            DefaultValue: null,
            Nullable: true,
            ConfigurationKey: "SpeedTest:LibreSpeed:ServerId",
            Restricted: false,
            Min: 1,
            Max: null),

        new(
            Key: SettingKeys.SpeedTestLibreSpeedExecutablePath,
            Group: SettingGroupNames.SpeedTest,
            Type: SettingType.Text,
            DefaultValue: "librespeed-cli",
            Nullable: false,
            ConfigurationKey: "SpeedTest:LibreSpeed:ExecutablePath",
            Restricted: true),

        new(
            Key: SettingKeys.SpeedTestCloudflareTimeoutSeconds,
            Group: SettingGroupNames.SpeedTest,
            Type: SettingType.Integer,
            DefaultValue: 300,
            Nullable: false,
            ConfigurationKey: "SpeedTest:Cloudflare:TimeoutSeconds",
            Restricted: false,
            Min: 10,
            Max: 1800),

        new(
            Key: SettingKeys.SpeedTestCloudflareExecutablePath,
            Group: SettingGroupNames.SpeedTest,
            Type: SettingType.Text,
            DefaultValue: "cfspeedtest",
            Nullable: false,
            ConfigurationKey: "SpeedTest:Cloudflare:ExecutablePath",
            Restricted: true),

        new(
            Key: SettingKeys.SpeedTestOoklaTimeoutSeconds,
            Group: SettingGroupNames.SpeedTest,
            Type: SettingType.Integer,
            DefaultValue: 120,
            Nullable: false,
            ConfigurationKey: "SpeedTest:Ookla:TimeoutSeconds",
            Restricted: false,
            Min: 10,
            Max: 1800),

        new(
            Key: SettingKeys.SpeedTestOoklaServerId,
            Group: SettingGroupNames.SpeedTest,
            Type: SettingType.Integer,
            DefaultValue: null,
            Nullable: true,
            ConfigurationKey: "SpeedTest:Ookla:ServerId",
            Restricted: false,
            Min: 1,
            Max: null),

        new(
            Key: SettingKeys.SpeedTestOoklaExecutablePath,
            Group: SettingGroupNames.SpeedTest,
            Type: SettingType.Text,
            DefaultValue: null,
            Nullable: true,
            ConfigurationKey: "SpeedTest:Ookla:ExecutablePath",
            Restricted: true),

        new(
            Key: SettingKeys.SpeedTestOoklaAcceptLicense,
            Group: SettingGroupNames.SpeedTest,
            Type: SettingType.Boolean,
            DefaultValue: false,
            Nullable: false,
            ConfigurationKey: "SpeedTest:Ookla:AcceptLicense",
            Restricted: false),

        new(
            Key: SettingKeys.RetentionDays,
            Group: SettingGroupNames.Retention,
            Type: SettingType.Integer,
            DefaultValue: 365,
            Nullable: false,
            ConfigurationKey: "DataRetention:Days",
            Restricted: false,
            Min: 1,
            Max: null),
    ];

    private static readonly Dictionary<string, SettingDefinition> ByKeyLookup =
        All.ToDictionary(def => def.Key, StringComparer.Ordinal);

    public static SettingDefinition? Find(string key) =>
        ByKeyLookup.GetValueOrDefault(key);
}
