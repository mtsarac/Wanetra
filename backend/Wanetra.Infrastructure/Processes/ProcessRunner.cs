using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Wanetra.Infrastructure.Processes;

internal sealed class ProcessRunner(ILogger<ProcessRunner> logger) : IProcessRunner
{
    public async Task<ProcessResult> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            // Arguments are passed as a list, never through a shell, so no
            // quoting or escaping of user input is involved.
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };

        logger.LogDebug("Starting {FileName} with {ArgumentCount} arguments", fileName, arguments.Count);
        process.Start();

        var standardOutput = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
        var standardError = process.StandardError.ReadToEndAsync(CancellationToken.None);

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);

        var timedOut = false;
        try
        {
            await process.WaitForExitAsync(timeoutSource.Token);
        }
        catch (OperationCanceledException)
        {
            timedOut = !cancellationToken.IsCancellationRequested;
            Kill(process, fileName);

            if (!timedOut)
            {
                throw;
            }
        }

        // The streams close once the process is gone, so these complete either way.
        var output = await standardOutput;
        var error = await standardError;

        if (timedOut)
        {
            throw new TimeoutException(
                $"{fileName} did not finish within {timeout.TotalSeconds:0} seconds and was terminated.");
        }

        return new ProcessResult(process.ExitCode, output, error);
    }

    private void Kill(Process process, string fileName)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not terminate {FileName}", fileName);
        }
    }
}
