namespace Wanetra.Domain;

public interface IOutageRepository
{
    Task<Outage?> GetOpenOutageAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<Outage>> GetRecentOutagesAsync(int count, CancellationToken cancellationToken);
    Task<IReadOnlyList<Outage>> GetOutagesOverlappingAsync(DateTime from, DateTime to, CancellationToken cancellationToken);
    void AddOutage(Outage outage);
    Task<int> DeleteOutagesOlderThanAsync(DateTime cutoff, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
