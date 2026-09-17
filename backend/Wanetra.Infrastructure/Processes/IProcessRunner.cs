namespace Wanetra.Infrastructure.Processes;

public interface IProcessRunner
{
    /// <summary>
    /// Runs an external command without a shell and captures its output.
    /// Throws <see cref="TimeoutException"/> when the process outlives
    /// <paramref name="timeout"/>.
    /// </summary>
    Task<ProcessResult> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken);
}
