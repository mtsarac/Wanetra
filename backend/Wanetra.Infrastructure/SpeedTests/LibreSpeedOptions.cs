namespace Wanetra.Infrastructure.SpeedTests;

public sealed class LibreSpeedOptions
{
    public const string SectionName = "SpeedTest:LibreSpeed";

    public string ExecutablePath { get; set; } = "librespeed-cli";

    public int? ServerId { get; set; }

    public int TimeoutSeconds { get; set; } = 120;
}
