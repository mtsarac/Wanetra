namespace Wanetra.Infrastructure.SpeedTests;

public sealed class OoklaOptions
{
    public const string SectionName = "SpeedTest:Ookla";

    // The Ookla binary is proprietary and cannot be redistributed, so it is never bundled in
    // the Wanetra image. Install it yourself and point this at it.
    public string ExecutablePath { get; set; } = "speedtest";

    public int? ServerId { get; set; }

    public int TimeoutSeconds { get; set; } = 120;

    // The CLI refuses to run non-interactively until the Ookla EULA and GDPR notice are accepted.
    // Setting this to true is the operator confirming they accepted them.
    public bool AcceptLicense { get; set; }
}
