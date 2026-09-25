namespace Wanetra.Domain;

public class AlertRule
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public bool Enabled { get; set; } = true;

    public double? MinDownloadMbps { get; set; }
    public double? MinUploadMbps { get; set; }
    public double? MaxLatencyMs { get; set; }
    public double? MaxJitterMs { get; set; }
    public double? MaxPacketLossPercent { get; set; }

    public double? DownloadBaselineDropPercent { get; set; }
    public double? UploadBaselineDropPercent { get; set; }

    public int ConsecutiveFailuresRequired { get; set; } = 3;
    public int ConsecutiveRecoveriesRequired { get; set; } = 2;

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
