namespace Wanetra.Domain;

public class SpeedTestResult
{
    public long Id { get; set; }
    public DateTime Timestamp { get; set; }
    public required string Engine { get; set; }

    // Engines may not report every metric, so measurements are nullable.
    public double? DownloadMbps { get; set; }
    public double? UploadMbps { get; set; }
    public double? LatencyMs { get; set; }
    public double? JitterMs { get; set; }
    public double? PacketLossPercent { get; set; }

    public string? ServerName { get; set; }
    public string? ServerLocation { get; set; }
    public string? ServerId { get; set; }
    public string? Isp { get; set; }
    public string? ExternalIp { get; set; }

    public long? DurationMs { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}
