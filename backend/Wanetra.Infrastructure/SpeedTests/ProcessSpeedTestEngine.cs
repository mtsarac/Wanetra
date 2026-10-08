using System.ComponentModel;
using System.Text.Json;
using Wanetra.Application.SpeedTests;
using Wanetra.Domain;
using Wanetra.Infrastructure.Processes;

namespace Wanetra.Infrastructure.SpeedTests;

/// <summary>
/// Runs an external speed test CLI and classifies every way that can fail. Subclasses only
/// describe the command line and how to turn its standard output into a result.
/// </summary>
internal abstract class ProcessSpeedTestEngine(IProcessRunner processRunner) : ISpeedTestEngine
{
    public abstract string Name { get; }

    protected abstract string ToolName { get; }

    protected abstract TimeSpan ProcessTimeout { get; }

    /// <summary>Resolves the executable to run; engines that provision their own binary do it here.</summary>
    protected abstract ValueTask<string> ResolveExecutableAsync(CancellationToken cancellationToken);

    protected abstract IReadOnlyList<string> BuildArguments();

    /// <summary>
    /// Maps the CLI's standard output. Throw <see cref="InvalidOperationException"/> or
    /// <see cref="JsonException"/> when the output cannot be used.
    /// </summary>
    protected abstract SpeedTestResult Map(string standardOutput);

    public async Task<SpeedTestResult> RunAsync(CancellationToken cancellationToken)
    {
        var executablePath = await ResolveExecutableAsync(cancellationToken);

        ProcessResult process;
        try
        {
            process = await processRunner.RunAsync(
                executablePath,
                BuildArguments(),
                ProcessTimeout,
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
                $"{ToolName} did not exit before the process timeout.",
                exception);
        }
        catch (Exception exception) when (exception is Win32Exception or UnauthorizedAccessException or IOException)
        {
            throw new SpeedTestExecutionException(
                SpeedTestFailureKind.LocalExecutionFailure,
                $"Could not start {ToolName}; verify that it exists and is executable.",
                exception);
        }

        if (process.ExitCode != 0)
        {
            var networkFailure = IsNetworkFailure(process.StandardError);
            throw new SpeedTestExecutionException(
                networkFailure ? SpeedTestFailureKind.NetworkFailure : SpeedTestFailureKind.LocalExecutionFailure,
                networkFailure
                    ? $"{ToolName} could not reach the test service."
                    : $"{ToolName} exited unexpectedly.");
        }

        try
        {
            return Map(process.StandardOutput);
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException)
        {
            throw new SpeedTestExecutionException(
                SpeedTestFailureKind.MeasurementFailure,
                $"{ToolName} returned invalid measurement output.",
                exception);
        }
    }

    protected static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool IsNetworkFailure(string error)
    {
        var text = error.ToLowerInvariant();
        return NetworkFailureMarkers.Any(marker => text.Contains(marker, StringComparison.Ordinal));
    }

    private static readonly string[] NetworkFailureMarkers =
    [
        "network is unreachable",
        "no route to host",
        "name or service not known",
        "temporary failure in name resolution",
        "no such host",
        "lookup ",
        "dns",
        "failed to resolve",
        "timed out",
        "timeout",
        "deadline exceeded",
        "connection refused",
        "connection reset",
        "connection aborted",
        "remote host closed",
        "no server available",
        "error sending request",
        "cannot open socket",
        "couldn't connect to server",
        "cannot retrieve configuration",
    ];
}
