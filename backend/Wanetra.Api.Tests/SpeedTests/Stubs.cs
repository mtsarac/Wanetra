using Wanetra.Application.Notifications;
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
    public Task<SpeedTestResult?> FindLatestSuccessfulBeforeAsync(DateTime toExclusive, CancellationToken cancellationToken) =>
        Task.FromResult(Results.LastOrDefault(result => result.Success && result.Timestamp < toExclusive));

    public Task<IReadOnlyList<SpeedTestResult>> FindSuccessfulSinceAsync(
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<SpeedTestResult>>(
            Results.Where(result => result.Success && result.Timestamp >= from && result.Timestamp <= to).ToList());
    public Task<IReadOnlyList<SpeedTestResult>> FindSuccessfulBeforeAsync(
        DateTime from,
        DateTime toExclusive,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<SpeedTestResult>>(
            Results.Where(result => result.Success && result.Timestamp >= from && result.Timestamp < toExclusive).ToList());


    public Task<int> DeleteOlderThanAsync(DateTime cutoff, CancellationToken cancellationToken)
    {
        var deleted = Results.RemoveAll(result => result.Timestamp < cutoff);
        return Task.FromResult(deleted);
    }
    public Task<SpeedTestResultPage> QueryAsync(
        SpeedTestResultQuery query,
        CancellationToken cancellationToken) =>
        Task.FromResult(new SpeedTestResultPage(Results, query.Page, query.PageSize, Results.Count));
}

internal sealed class RecordingAlertStateRepository : IAlertStateRepository
{
    private readonly AlertState state = new() { Id = 1, UpdatedAt = DateTime.UtcNow };

    public Task<AlertRule?> GetEnabledRuleAsync(CancellationToken cancellationToken) => Task.FromResult<AlertRule?>(null);

    public Task<AlertRule?> GetRuleAsync(CancellationToken cancellationToken) => Task.FromResult<AlertRule?>(null);

    public Task<AlertState> GetStateAsync(CancellationToken cancellationToken) => Task.FromResult(state);

    public Task<DegradationEvent?> GetOpenEventAsync(CancellationToken cancellationToken) => Task.FromResult<DegradationEvent?>(null);

    public Task<DegradationEvent?> GetEventAsync(long id, CancellationToken cancellationToken) => Task.FromResult<DegradationEvent?>(null);
    public Task<IReadOnlyList<DegradationEvent>> GetRecentEventsAsync(int count, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<DegradationEvent>>([]);
    public Task<IReadOnlyList<DegradationEvent>> GetPendingNotificationEventsAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<DegradationEvent>>([]);

    public void AddRule(AlertRule rule) { }

    public void AddEvent(DegradationEvent degradationEvent) { }
    public void AddNotificationDelivery(NotificationDelivery delivery) { }

    public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
internal sealed class RecordingNotificationConfigurationRepository : INotificationConfigurationRepository
{
    public Task<IReadOnlyList<NotificationConfiguration>> ListAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<NotificationConfiguration>>([]);

    public Task SaveAsync(IReadOnlyList<NotificationConfiguration> configurations, CancellationToken cancellationToken) =>
        Task.CompletedTask;
}
internal sealed class StubNotificationProvider(string name) : INotificationProvider
{
    public string Name => name;

    public Task SendAsync(NotificationMessage message, string configurationJson, CancellationToken cancellationToken) =>
        Task.CompletedTask;
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
