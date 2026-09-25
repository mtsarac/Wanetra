using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Wanetra.Application.SpeedTests;
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

        ProcessResult process;
        try
        {
            process = await processRunner.RunAsync(
                settings.ExecutablePath,
                arguments,
                TimeSpan.FromSeconds(settings.TimeoutSeconds),
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (TimeoutException exception)
        {
            throw new SpeedTestExecutionException(
                SpeedTestFailureKind.LocalExecutionFailure,
                "librespeed-cli did not exit before the process timeout.",
                exception);
        }
        catch (Exception exception) when (exception is Win32Exception or UnauthorizedAccessException or IOException)
        {
            throw new SpeedTestExecutionException(
                SpeedTestFailureKind.LocalExecutionFailure,
                "Could not start librespeed-cli; verify that it exists and is executable.",
                exception);
        }

        if (process.ExitCode != 0)
        {
            var networkFailure = IsNetworkFailure(process.StandardError);
            throw new SpeedTestExecutionException(
                networkFailure ? SpeedTestFailureKind.NetworkFailure : SpeedTestFailureKind.LocalExecutionFailure,
                networkFailure
                    ? "librespeed-cli could not reach the test service."
                    : "librespeed-cli exited unexpectedly.");
        }

        try
        {
            return Map(Parse(process.StandardOutput), settings);
        }
        catch (SpeedTestExecutionException)
        {
            throw;
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException)
        {
            throw new SpeedTestExecutionException(
                SpeedTestFailureKind.MeasurementFailure,
                "librespeed-cli returned invalid measurement output.",
                exception);
        }
    }
    private static bool IsNetworkFailure(string error)
    {
        var text = error.ToLowerInvariant();
        return text.Contains("network is unreachable", StringComparison.Ordinal)
            || text.Contains("no route to host", StringComparison.Ordinal)
            || text.Contains("name or service not known", StringComparison.Ordinal)
            || text.Contains("temporary failure in name resolution", StringComparison.Ordinal)
            || text.Contains("no such host", StringComparison.Ordinal)
            || text.Contains("lookup ", StringComparison.Ordinal)
            || text.Contains("dns", StringComparison.Ordinal)
            || text.Contains("timed out", StringComparison.Ordinal)
            || text.Contains("timeout", StringComparison.Ordinal)
            || text.Contains("deadline exceeded", StringComparison.Ordinal)
            || text.Contains("connection refused", StringComparison.Ordinal)
            || text.Contains("connection reset", StringComparison.Ordinal)
            || text.Contains("connection aborted", StringComparison.Ordinal)
            || text.Contains("remote host closed", StringComparison.Ordinal)
            || text.Contains("no server available", StringComparison.Ordinal);
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

}
