using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Wanetra.Domain;
using Wanetra.Infrastructure.Processes;

namespace Wanetra.Infrastructure.SpeedTests;

/// <summary>
/// Wraps the official Ookla <c>speedtest</c> CLI. The binary is the operator's own: supplied via
/// <c>ExecutablePath</c>, or downloaded from Ookla on first use once the license is accepted.
/// </summary>
internal sealed class OoklaSpeedEngine(
    IProcessRunner processRunner,
    IOptions<OoklaOptions> options,
    IOoklaBinary binary) : ProcessSpeedTestEngine(processRunner)
{
    private const double BitsPerByte = 8;
    private const double BitsPerMegabit = 1_000_000;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public override string Name => "ookla";

    protected override string ToolName => "speedtest";

    protected override TimeSpan ProcessTimeout => TimeSpan.FromSeconds(options.Value.TimeoutSeconds);

    protected override ValueTask<string> ResolveExecutableAsync(CancellationToken cancellationToken) =>
        string.IsNullOrWhiteSpace(options.Value.ExecutablePath)
            ? new ValueTask<string>(binary.EnsureInstalledAsync(cancellationToken))
            : ValueTask.FromResult(options.Value.ExecutablePath);

    protected override IReadOnlyList<string> BuildArguments()
    {
        var arguments = new List<string> { "--format=json", "--progress=no", "--accept-license", "--accept-gdpr" };
        if (options.Value.ServerId is { } serverId)
        {
            arguments.Add($"--server-id={serverId.ToString(CultureInfo.InvariantCulture)}");
        }

        return arguments;
    }

    protected override SpeedTestResult Map(string standardOutput)
    {
        var output = JsonSerializer.Deserialize<OoklaOutput>(standardOutput, JsonOptions)
            ?? throw new InvalidOperationException("speedtest returned no measurement.");

        var server = output.Server;
        var location = string.Join(", ", new[] { server?.Location, server?.Country }
            .Select(Normalize)
            .Where(part => part is not null));

        return new SpeedTestResult
        {
            Engine = Name,
            DownloadMbps = ToMbps(output.Download, "download"),
            UploadMbps = ToMbps(output.Upload, "upload"),
            LatencyMs = output.Ping?.Latency,
            JitterMs = output.Ping?.Jitter,

            // Absent when the CLI cannot measure it in the current network environment.
            PacketLossPercent = output.PacketLoss,

            ServerName = Normalize(server?.Name),
            ServerLocation = location.Length == 0 ? null : location,
            ServerId = server?.Id?.ToString(CultureInfo.InvariantCulture),
            Isp = Normalize(output.Isp),
            ExternalIp = Normalize(output.Interface?.ExternalIp),
        };
    }

    private static double ToMbps(OoklaTransfer? transfer, string direction) =>
        transfer?.Bandwidth is { } bytesPerSecond && double.IsFinite(bytesPerSecond)
            ? bytesPerSecond * BitsPerByte / BitsPerMegabit
            : throw new InvalidOperationException($"speedtest returned no {direction} bandwidth.");
}
