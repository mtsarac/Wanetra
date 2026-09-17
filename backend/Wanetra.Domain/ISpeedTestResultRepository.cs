namespace Wanetra.Domain;

public interface ISpeedTestResultRepository
{
    Task AddAsync(SpeedTestResult result, CancellationToken cancellationToken);

    Task<SpeedTestResult?> FindAsync(long id, CancellationToken cancellationToken);

    /// <summary>Most recent result, successful or not, or <c>null</c> when none exists.</summary>
    Task<SpeedTestResult?> FindLatestAsync(CancellationToken cancellationToken);

    /// <summary>Reads one page of history. Never materializes the whole table.</summary>
    Task<SpeedTestResultPage> QueryAsync(SpeedTestResultQuery query, CancellationToken cancellationToken);
}
