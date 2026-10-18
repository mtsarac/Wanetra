namespace Wanetra.Domain;

public class ProbeSample
{
    public long Id { get; set; }
    public int TargetId { get; set; }
    public DateTime Timestamp { get; set; }
    public bool Success { get; set; }
    public double? RttMs { get; set; }
    public string? Error { get; set; }

    // The link was busy with a speed test, so latency and loss say little about the connection itself.
    public bool DuringSpeedTest { get; set; }
}
