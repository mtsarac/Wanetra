using Wanetra.Domain;
using Wanetra.Infrastructure.Processes;

namespace Wanetra.Api.Tests.SpeedTests;

internal sealed class StubSpeedTestEngine(Func<CancellationToken, Task<SpeedTestResult>> run) : ISpeedTestEngine
{
    public string Name => "stub";

    public Task<SpeedTestResult> RunAsync(CancellationToken cancellationToken) => run(cancellationToken);
}

internal sealed class RecordingResultRepository : ISpeedTestResultRepository
{
    public List<SpeedTestResult> Results { get; } = [];

    public Task AddAsync(SpeedTestResult result, CancellationToken cancellationToken)
    {
        Results.Add(result);
        return Task.CompletedTask;
    }
}

internal sealed class StubProcessRunner(ProcessResult result) : IProcessRunner
{
    public IReadOnlyList<string> Arguments { get; private set; } = [];
    public string? FileName { get; private set; }

    public Task<ProcessResult> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        FileName = fileName;
        Arguments = arguments;
        return Task.FromResult(result);
    }
}
