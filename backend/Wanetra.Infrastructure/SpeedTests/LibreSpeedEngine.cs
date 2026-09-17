using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Wanetra.Domain;
using Wanetra.Infrastructure.Processes;

namespace Wanetra.Infrastructure.SpeedTests;

/// <summary>
/// Wraps the librespeed-cli process. Wanetra does not measure bandwidth itself.
/// </summary>
internal sealed class LibreSpeedEngine(
    IProcessRunner processRunner,
    IOptions<LibreSpeedOptions> options) : ISpeedTestEngine
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public string Name => "librespeed";

    public async Task<SpeedTestResult> RunAsync(CancellationToken cancellationToken)
    {
        var settings = options.Value;

        var arguments = new List<string> { "--json" };
        if (settings.ServerId is { } serverId)
        {
            arguments.Add("--server");
            arguments.Add(serverId.ToString(CultureInfo.InvariantCulture));
        }

        var process = await processRunner.RunAsync(
            settings.ExecutablePath,
            arguments,
            TimeSpan.FromSeconds(settings.TimeoutSeconds),
            cancellationToken);

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"librespeed-cli exited with code {process.ExitCode}: {Describe(process.StandardError)}");
        }

        return Map(Parse(process.StandardOutput), settings);
    }

    private static LibreSpeedOutput Parse(string standardOutput)
    {
        LibreSpeedOutput[]? entries;
        try
        {
            entries = JsonSerializer.Deserialize<LibreSpeedOutput[]>(standardOutput, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"librespeed-cli returned unreadable output: {ex.Message}", ex);
        }

        return entries is [var entry, ..]
            ? entry
            : throw new InvalidOperationException("librespeed-cli returned no measurement.");
    }

    private SpeedTestResult Map(LibreSpeedOutput output, LibreSpeedOptions settings) => new()
    {
        Engine = Name,
        DownloadMbps = output.Download,
        UploadMbps = output.Upload,
        LatencyMs = output.Ping,
        JitterMs = output.Jitter,

        // LibreSpeed reports no packet loss.
        PacketLossPercent = null,

        ServerName = Normalize(output.Server?.Name),
        ServerId = settings.ServerId?.ToString(CultureInfo.InvariantCulture),
        Isp = Normalize(output.Client?.Org),
        ExternalIp = Normalize(output.Client?.Ip),
    };

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string Describe(string standardError)
    {
        var message = standardError.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .LastOrDefault();

        return string.IsNullOrEmpty(message) ? "no error output" : message;
    }
}
