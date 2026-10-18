using Microsoft.EntityFrameworkCore;
using Wanetra.Domain;

namespace Wanetra.Infrastructure.Persistence;

internal sealed class OutageRepository(WanetraDbContext dbContext) : IOutageRepository
{
    public Task<Outage?> GetOpenOutageAsync(CancellationToken cancellationToken) =>
        dbContext.Outages
            .Include(outage => outage.NotificationDeliveries)
            .Where(outage => outage.Status == OutageStatus.Active)
            .OrderBy(outage => outage.StartedAt)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<Outage>> GetRecentOutagesAsync(int count, CancellationToken cancellationToken) =>
        await dbContext.Outages
            .AsNoTracking()
            .Include(outage => outage.NotificationDeliveries)
            .OrderByDescending(outage => outage.StartedAt)
            .ThenByDescending(outage => outage.Id)
            .Take(count)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Outage>> GetOutagesOverlappingAsync(
        DateTime from, DateTime to, CancellationToken cancellationToken) =>
        await dbContext.Outages
            .AsNoTracking()
            .Where(outage => outage.StartedAt < to && (outage.EndedAt == null || outage.EndedAt > from))
            .OrderBy(outage => outage.StartedAt)
            .ToListAsync(cancellationToken);

    public void AddOutage(Outage outage) => dbContext.Outages.Add(outage);

    public Task<int> DeleteOutagesOlderThanAsync(DateTime cutoff, CancellationToken cancellationToken) =>
        dbContext.Outages
            .Where(outage => outage.Status == OutageStatus.Recovered && outage.EndedAt != null && outage.EndedAt < cutoff)
            .ExecuteDeleteAsync(cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => dbContext.SaveChangesAsync(cancellationToken);
}
