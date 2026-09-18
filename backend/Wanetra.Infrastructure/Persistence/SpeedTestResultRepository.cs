using Microsoft.EntityFrameworkCore;
using Wanetra.Domain;

namespace Wanetra.Infrastructure.Persistence;

internal sealed class SpeedTestResultRepository(WanetraDbContext dbContext) : ISpeedTestResultRepository
{
    public async Task AddAsync(SpeedTestResult result, CancellationToken cancellationToken)
    {
        dbContext.SpeedTestResults.Add(result);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<SpeedTestResult?> FindAsync(long id, CancellationToken cancellationToken) =>
        dbContext.SpeedTestResults
            .AsNoTracking()
            .SingleOrDefaultAsync(result => result.Id == id, cancellationToken);

    public Task<SpeedTestResult?> FindLatestAsync(CancellationToken cancellationToken) =>
        dbContext.SpeedTestResults
            .AsNoTracking()
            .OrderByDescending(result => result.Timestamp)
            .ThenByDescending(result => result.Id)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<SpeedTestResultPage> QueryAsync(
        SpeedTestResultQuery query,
        CancellationToken cancellationToken)
    {
        var filtered = Filter(dbContext.SpeedTestResults.AsNoTracking(), query);

        var totalCount = await filtered.CountAsync(cancellationToken);

        var ordered = query.NewestFirst
            ? filtered.OrderByDescending(result => result.Timestamp).ThenByDescending(result => result.Id)
            : filtered.OrderBy(result => result.Timestamp).ThenBy(result => result.Id);

        var items = await ordered
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return new SpeedTestResultPage(items, query.Page, query.PageSize, totalCount);
    }

    private static IQueryable<SpeedTestResult> Filter(
        IQueryable<SpeedTestResult> results,
        SpeedTestResultQuery query)
    {
        if (query.From is { } from)
        {
            results = results.Where(result => result.Timestamp >= from);
        }

        if (query.To is { } to)
        {
            results = results.Where(result => result.Timestamp <= to);
        }

        if (query.Success is { } success)
        {
            results = results.Where(result => result.Success == success);
        }

        if (query.Engine is { } engine)
        {
            results = results.Where(result => result.Engine == engine);
        }

        return results;
    }
}
