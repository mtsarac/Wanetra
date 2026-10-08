using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wanetra.Application.SpeedTests;
using Wanetra.Domain;
using Wanetra.Infrastructure.Processes;

namespace Wanetra.Infrastructure.SpeedTests;

/// <summary>
/// Wraps the official Ookla <c>speedtest</c> CLI. The binary comes from Ookla on first use once the
/// license is accepted; <c>ExecutablePath</c> is only the fallback when that download is not possible.
/// </summary>
internal sealed class OoklaSpeedEngine(
    IProcessRunner processRunner,
    IOptions<OoklaOptions> options,
    IOoklaBinary binary,
    ILogger<OoklaSpeedEngine> logger) : ProcessSpeedTestEngine(processRunner)
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

    protected override async ValueTask<string> ResolveExecutableAsync(CancellationToken cancellationToken)
    {
        if (!options.Value.AcceptLicense)
        {
            throw new SpeedTestExecutionException(
                SpeedTestFailureKind.LocalExecutionFailure,
                "The Ookla speed test engine requires license acceptance. Set speedtest.ookla.acceptLicense to true.");
        }

        try
        {
            return await binary.EnsureInstalledAsync(cancellationToken);
        }
        catch (SpeedTestExecutionException exception) when (!string.IsNullOrWhiteSpace(options.Value.ExecutablePath))
        {
            logger.LogWarning(
                "{Reason}; using the fallback {ExecutablePath}",
                exception.InnerException is null ? exception.Message : $"{exception.Message} ({exception.InnerException.Message})",
                options.Value.ExecutablePath);
            return options.Value.ExecutablePath;
        }
    }

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
