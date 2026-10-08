namespace Wanetra.Infrastructure.SpeedTests;

// Mirrors the subset of `cfspeedtest -o json` that Wanetra reads (snake_case on the wire).
internal sealed class CloudflareOutput
{
    public CloudflareMetadata? Metadata { get; set; }
    public CloudflareLatency? LatencyMeasurement { get; set; }
    public List<CloudflareSpeedMeasurement>? SpeedMeasurements { get; set; }
}

internal sealed class CloudflareMetadata
{
    public string? Country { get; set; }
    public string? Ip { get; set; }
    public string? Colo { get; set; }
}

internal sealed class CloudflareLatency
{
    public List<double>? LatencyMeasurements { get; set; }
}

internal sealed class CloudflareSpeedMeasurement
{
    public string? TestType { get; set; }
    public long PayloadSize { get; set; }
    public double? Median { get; set; }
    public int Successes { get; set; }
}
