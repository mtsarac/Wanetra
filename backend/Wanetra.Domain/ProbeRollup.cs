namespace Wanetra.Domain;

public enum ProbeResolution
{
    Minute,
    Hour,
}

public class ProbeRollup
{
    public long Id { get; set; }
    public int TargetId { get; set; }
    public ProbeResolution Resolution { get; set; }
    public DateTime BucketStart { get; set; }
    public int Sent { get; set; }
    public int Received { get; set; }
    public double? RttMinMs { get; set; }
    public double? RttAvgMs { get; set; }
    public double? RttMaxMs { get; set; }
    public double? JitterMs { get; set; }

    public double LossPercent => Sent == 0 ? 0 : (Sent - Received) * 100.0 / Sent;
}
