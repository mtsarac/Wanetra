namespace Wanetra.Infrastructure.SpeedTests;

/// <summary>
/// Shape of one entry in the JSON array that <c>librespeed-cli --json</c> prints.
/// Speeds are Mbps, ping and jitter are milliseconds.
/// </summary>
internal sealed class LibreSpeedOutput
{
    public double Download { get; set; }
    public double Upload { get; set; }
    public double Ping { get; set; }
    public double Jitter { get; set; }

    public LibreSpeedServer? Server { get; set; }
    public LibreSpeedClient? Client { get; set; }
}

internal sealed class LibreSpeedServer
{
    public string? Name { get; set; }
}

internal sealed class LibreSpeedClient
{
    public string? Ip { get; set; }
    public string? Org { get; set; }
}
