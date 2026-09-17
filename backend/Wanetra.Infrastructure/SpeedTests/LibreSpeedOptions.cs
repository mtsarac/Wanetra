namespace Wanetra.Infrastructure.SpeedTests;

public sealed class LibreSpeedOptions
{
    public const string SectionName = "SpeedTest:LibreSpeed";

    /// <summary>
    /// Path to the librespeed-cli binary, or its name when it is on PATH.
    /// </summary>
    public string ExecutablePath { get; set; } = "librespeed-cli";

    /// <summary>
    /// Server to test against. LibreSpeed picks the closest one when unset.
    /// </summary>
    public int? ServerId { get; set; }

    public int TimeoutSeconds { get; set; } = 120;
}
