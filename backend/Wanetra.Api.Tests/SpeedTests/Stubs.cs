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
    public List<SpeedTestResult> Results { get; init; } = [];

    public Task AddAsync(SpeedTestResult result, CancellationToken cancellationToken)
    {
        Results.Add(result);
        return Task.CompletedTask;
    }

    public Task<SpeedTestResult?> FindAsync(long id, CancellationToken cancellationToken) =>
        Task.FromResult(Results.FirstOrDefault(result => result.Id == id));

    public Task<SpeedTestResult?> FindLatestAsync(CancellationToken cancellationToken) =>
        Task.FromResult(Results.LastOrDefault());

    public Task<SpeedTestResult?> FindLatestSuccessfulAsync(CancellationToken cancellationToken) =>
        Task.FromResult(Results.LastOrDefault(result => result.Success));

    public Task<IReadOnlyList<SpeedTestResult>> FindSuccessfulSinceAsync(
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<SpeedTestResult>>(
            Results.Where(result => result.Success && result.Timestamp >= from && result.Timestamp <= to).ToList());

    public Task<SpeedTestResultPage> QueryAsync(
        SpeedTestResultQuery query,
        CancellationToken cancellationToken) =>
        Task.FromResult(new SpeedTestResultPage(Results, query.Page, query.PageSize, Results.Count));
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
