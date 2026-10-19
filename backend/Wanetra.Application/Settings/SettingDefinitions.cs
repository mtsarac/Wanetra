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
    public const string LanEnabled = "lan.enabled";
    public const string LanScanIntervalMinutes = "lan.scanIntervalMinutes";
    public const string LanCidr = "lan.cidr";
    public const string LanOfflineAfterScans = "lan.offlineAfterScans";
    public const string LanAlertOnNewDevice = "lan.alertOnNewDevice";
    public const string ProbesEnabled = "probes.enabled";
    public const string ProbesIntervalSeconds = "probes.intervalSeconds";
    public const string ProbesTimeoutMs = "probes.timeoutMs";
    public const string ProbesOutageFailuresRequired = "probes.outage.failuresRequired";
    public const string ProbesOutageRecoveriesRequired = "probes.outage.recoveriesRequired";
    public const string ProbesRawRetentionHours = "probes.retention.rawHours";
    public const string ProbesMinuteRetentionDays = "probes.retention.minuteDays";
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

        new(
            Key: SettingKeys.LanEnabled,
            Group: SettingGroupNames.Lan,
            Type: SettingType.Boolean,
            DefaultValue: false,
            Nullable: false,
            ConfigurationKey: "Lan:Enabled",
            Restricted: false),

        new(
            Key: SettingKeys.LanScanIntervalMinutes,
            Group: SettingGroupNames.Lan,
            Type: SettingType.Integer,
            DefaultValue: 5,
            Nullable: false,
            ConfigurationKey: "Lan:ScanIntervalMinutes",
            Restricted: false,
            Min: 1,
            Max: 1440),

        new(
            Key: SettingKeys.LanCidr,
            Group: SettingGroupNames.Lan,
            Type: SettingType.Text,
            DefaultValue: null,
            Nullable: true,
            ConfigurationKey: "Lan:Cidr",
            Restricted: false),

        new(
            Key: SettingKeys.LanOfflineAfterScans,
            Group: SettingGroupNames.Lan,
            Type: SettingType.Integer,
            DefaultValue: 3,
            Nullable: false,
            ConfigurationKey: "Lan:OfflineAfterScans",
            Restricted: false,
            Min: 1,
            Max: 20),

        new(
            Key: SettingKeys.LanAlertOnNewDevice,
            Group: SettingGroupNames.Lan,
            Type: SettingType.Boolean,
            DefaultValue: true,
            Nullable: false,
            ConfigurationKey: "Lan:AlertOnNewDevice",
            Restricted: false),

        new(
            Key: SettingKeys.ProbesEnabled,
            Group: SettingGroupNames.Probes,
            Type: SettingType.Boolean,
            DefaultValue: true,
            Nullable: false,
            ConfigurationKey: "Probes:Enabled",
            Restricted: false),

        new(
            Key: SettingKeys.ProbesIntervalSeconds,
            Group: SettingGroupNames.Probes,
            Type: SettingType.Integer,
            DefaultValue: 10,
            Nullable: false,
            ConfigurationKey: "Probes:IntervalSeconds",
            Restricted: false,
            Min: 5,
            Max: 300),

        new(
            Key: SettingKeys.ProbesTimeoutMs,
            Group: SettingGroupNames.Probes,
            Type: SettingType.Integer,
            DefaultValue: 2000,
            Nullable: false,
            ConfigurationKey: "Probes:TimeoutMs",
            Restricted: false,
            Min: 200,
            Max: 10000),

        new(
            Key: SettingKeys.ProbesOutageFailuresRequired,
            Group: SettingGroupNames.Probes,
            Type: SettingType.Integer,
            DefaultValue: 3,
            Nullable: false,
            ConfigurationKey: "Probes:Outage:FailuresRequired",
            Restricted: false,
            Min: 1,
            Max: 20),

        new(
            Key: SettingKeys.ProbesOutageRecoveriesRequired,
            Group: SettingGroupNames.Probes,
            Type: SettingType.Integer,
            DefaultValue: 3,
            Nullable: false,
            ConfigurationKey: "Probes:Outage:RecoveriesRequired",
            Restricted: false,
            Min: 1,
            Max: 20),

        new(
            Key: SettingKeys.ProbesRawRetentionHours,
            Group: SettingGroupNames.Probes,
            Type: SettingType.Integer,
            DefaultValue: 48,
            Nullable: false,
            ConfigurationKey: "Probes:Retention:RawHours",
            Restricted: false,
            Min: 1,
            Max: 720),

        new(
            Key: SettingKeys.ProbesMinuteRetentionDays,
            Group: SettingGroupNames.Probes,
            Type: SettingType.Integer,
            DefaultValue: 30,
            Nullable: false,
            ConfigurationKey: "Probes:Retention:MinuteDays",
            Restricted: false,
            Min: 1,
            Max: 365),
    ];

    private static readonly Dictionary<string, SettingDefinition> ByKeyLookup =
        All.ToDictionary(def => def.Key, StringComparer.Ordinal);

    public static SettingDefinition? Find(string key) =>
        ByKeyLookup.GetValueOrDefault(key);
}
