namespace Wanetra.Infrastructure.SpeedTests;

// Mirrors the subset of `speedtest --format=json` that Wanetra reads.
internal sealed class OoklaOutput
{
    public OoklaPing? Ping { get; set; }
    public OoklaTransfer? Download { get; set; }
    public OoklaTransfer? Upload { get; set; }
    public double? PacketLoss { get; set; }
    public string? Isp { get; set; }
    public OoklaInterface? Interface { get; set; }
    public OoklaServer? Server { get; set; }
}

internal sealed class OoklaPing
{
    public double? Latency { get; set; }
    public double? Jitter { get; set; }
}

internal sealed class OoklaTransfer
{
    // Bytes per second in machine-readable output.
    public double? Bandwidth { get; set; }
}

internal sealed class OoklaInterface
{
    public string? ExternalIp { get; set; }
}

internal sealed class OoklaServer
{
    public long? Id { get; set; }
    public string? Name { get; set; }
    public string? Location { get; set; }
    public string? Country { get; set; }
}
