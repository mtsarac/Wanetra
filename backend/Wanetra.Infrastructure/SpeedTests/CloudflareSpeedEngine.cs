using System.Text.Json;
using Microsoft.Extensions.Options;
using Wanetra.Domain;
using Wanetra.Infrastructure.Processes;

namespace Wanetra.Infrastructure.SpeedTests;

/// <summary>
/// Wraps <c>cfspeedtest</c>, an unofficial CLI for speed.cloudflare.com.
/// Cloudflare reports no packet loss without a TURN server, so that metric stays unavailable.
/// </summary>
internal sealed class CloudflareSpeedEngine(
    IProcessRunner processRunner,
    IOptions<CloudflareOptions> options) : ProcessSpeedTestEngine(processRunner)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
    };

    public override string Name => "cloudflare";

    protected override string ToolName => "cfspeedtest";

    protected override string ExecutablePath => options.Value.ExecutablePath;

    protected override TimeSpan ProcessTimeout => TimeSpan.FromSeconds(options.Value.TimeoutSeconds);

    protected override IReadOnlyList<string> BuildArguments() => ["--output-format", "json"];

    protected override SpeedTestResult Map(string standardOutput)
    {
        var output = JsonSerializer.Deserialize<CloudflareOutput>(standardOutput, JsonOptions)
            ?? throw new InvalidOperationException("cfspeedtest returned no measurement.");

        var latencies = output.LatencyMeasurement?.LatencyMeasurements ?? [];
        var colo = Normalize(output.Metadata?.Colo);

        return new SpeedTestResult
        {
            Engine = Name,
            DownloadMbps = Throughput(output, "Download"),
            UploadMbps = Throughput(output, "Upload"),
            LatencyMs = latencies.Count > 0 ? Median(latencies) : null,
            JitterMs = latencies.Count > 1 ? MeanConsecutiveDifference(latencies) : null,

            PacketLossPercent = null,

            ServerName = colo is null ? "Cloudflare" : $"Cloudflare ({colo})",
            ServerLocation = Normalize(output.Metadata?.Country),
            ExternalIp = Normalize(output.Metadata?.Ip),
        };
    }

    // Small payloads are dominated by TCP slow start, so the headline figure is the median of
    // the largest payload size that produced samples; cfspeedtest drops larger sizes on slow links.
    private static double Throughput(CloudflareOutput output, string testType)
    {
        var headline = (output.SpeedMeasurements ?? [])
            .Where(measurement => string.Equals(measurement.TestType, testType, StringComparison.OrdinalIgnoreCase)
                && measurement.Successes > 0
                && measurement.Median is { } median
                && double.IsFinite(median))
            .MaxBy(measurement => measurement.PayloadSize);

        return headline?.Median
            ?? throw new InvalidOperationException($"cfspeedtest returned no {testType.ToLowerInvariant()} samples.");
    }

    private static double Median(List<double> values)
    {
        var sorted = values.Order().ToArray();
        var middle = sorted.Length / 2;
        return sorted.Length % 2 == 1 ? sorted[middle] : (sorted[middle - 1] + sorted[middle]) / 2;
    }

    // Jitter as Cloudflare defines it: the mean distance between consecutive latency samples.
    private static double MeanConsecutiveDifference(List<double> values) =>
        values.Zip(values.Skip(1), (previous, next) => Math.Abs(next - previous)).Average();
}
