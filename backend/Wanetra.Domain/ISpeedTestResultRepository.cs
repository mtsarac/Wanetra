namespace Wanetra.Domain;

public interface ISpeedTestResultRepository
{
    Task AddAsync(SpeedTestResult result, CancellationToken cancellationToken);

    Task<SpeedTestResult?> FindAsync(long id, CancellationToken cancellationToken);

    /// <summary>Most recent result, successful or not, or <c>null</c> when none exists.</summary>
    Task<SpeedTestResult?> FindLatestAsync(CancellationToken cancellationToken);

    Task<SpeedTestResult?> FindLatestSuccessfulAsync(CancellationToken cancellationToken);

    Task<SpeedTestResult?> FindLatestSuccessfulBeforeAsync(DateTime toExclusive, CancellationToken cancellationToken);

    Task<IReadOnlyList<SpeedTestResult>> FindSuccessfulSinceAsync(
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<SpeedTestResult>> FindSuccessfulBeforeAsync(
        DateTime from,
        DateTime toExclusive,
        CancellationToken cancellationToken);

    /// <summary>Deletes results strictly older than the UTC cutoff and returns the number removed.</summary>
    Task<int> DeleteOlderThanAsync(DateTime cutoff, CancellationToken cancellationToken);

    /// <summary>Reads one page of history. Never materializes the whole table.</summary>
    Task<SpeedTestResultPage> QueryAsync(SpeedTestResultQuery query, CancellationToken cancellationToken);
}
