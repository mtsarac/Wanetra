namespace Wanetra.Domain;

public interface ISpeedTestResultRepository
{
    Task AddAsync(SpeedTestResult result, CancellationToken cancellationToken);

    Task<SpeedTestResult?> FindAsync(long id, CancellationToken cancellationToken);

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

    Task<int> DeleteOlderThanAsync(DateTime cutoff, CancellationToken cancellationToken);

    Task<SpeedTestResultPage> QueryAsync(SpeedTestResultQuery query, CancellationToken cancellationToken);
}
