namespace Wanetra.Infrastructure.SpeedTests;

public sealed class OoklaOptions
{
    public const string SectionName = "SpeedTest:Ookla";

    // The Ookla binary is proprietary and is never bundled in the Wanetra image. Leave this unset
    // and Wanetra downloads the official CLI from Ookla into the data directory on first use.
    // Set it to run a binary you installed yourself (nothing is downloaded then).
    public string? ExecutablePath { get; set; }

    public int? ServerId { get; set; }

    public int TimeoutSeconds { get; set; } = 120;

    // The CLI refuses to run non-interactively until the Ookla EULA and GDPR notice are accepted.
    // Setting this to true is the operator confirming they accepted them, and it is also what
    // authorises the download above.
    public bool AcceptLicense { get; set; }
}
