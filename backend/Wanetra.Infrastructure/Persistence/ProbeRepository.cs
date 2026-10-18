using Microsoft.EntityFrameworkCore;
using Wanetra.Domain;

namespace Wanetra.Infrastructure.Persistence;

internal sealed class ProbeRepository(WanetraDbContext dbContext) : IProbeRepository
{
    public async Task<IReadOnlyList<ProbeTarget>> ListTargetsAsync(bool onlyEnabled, CancellationToken cancellationToken) =>
        await dbContext.ProbeTargets
            .Where(target => !onlyEnabled || target.Enabled)
            .OrderBy(target => target.Id)
            .ToListAsync(cancellationToken);

    public Task<ProbeTarget?> GetTargetAsync(int id, CancellationToken cancellationToken) =>
        dbContext.ProbeTargets.SingleOrDefaultAsync(target => target.Id == id, cancellationToken);

    public Task<int> CountTargetsAsync(CancellationToken cancellationToken) =>
        dbContext.ProbeTargets.CountAsync(cancellationToken);

    public void AddTarget(ProbeTarget target) => dbContext.ProbeTargets.Add(target);

    public void RemoveTarget(ProbeTarget target) => dbContext.ProbeTargets.Remove(target);

    public void AddSamples(IEnumerable<ProbeSample> samples) => dbContext.ProbeSamples.AddRange(samples);

    public async Task<IReadOnlyList<ProbeSample>> GetSamplesAsync(
        int targetId, DateTime from, DateTime to, CancellationToken cancellationToken) =>
        await dbContext.ProbeSamples
            .AsNoTracking()
            .Where(sample => sample.TargetId == targetId && sample.Timestamp >= from && sample.Timestamp <= to)
            .OrderBy(sample => sample.Timestamp)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ProbeSample>> GetSamplesSinceAsync(DateTime since, CancellationToken cancellationToken) =>
        await dbContext.ProbeSamples
            .AsNoTracking()
            .Where(sample => sample.Timestamp >= since)
            .OrderBy(sample => sample.Timestamp)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ProbeSample>> GetSamplesBetweenAsync(
        DateTime from, DateTime toExclusive, CancellationToken cancellationToken) =>
        await dbContext.ProbeSamples
            .AsNoTracking()
            .Where(sample => sample.Timestamp >= from && sample.Timestamp < toExclusive)
            .OrderBy(sample => sample.Timestamp)
            .ToListAsync(cancellationToken);

    public async Task<DateTime?> GetEarliestSampleAsync(CancellationToken cancellationToken)
    {
        var first = await dbContext.ProbeSamples
            .AsNoTracking()
            .OrderBy(sample => sample.Timestamp)
            .Select(sample => new { sample.Timestamp })
            .FirstOrDefaultAsync(cancellationToken);
        return first?.Timestamp;
    }

    public void AddRollups(IEnumerable<ProbeRollup> rollups) => dbContext.ProbeRollups.AddRange(rollups);

    public async Task<IReadOnlyList<ProbeRollup>> GetRollupsAsync(
        int? targetId, ProbeResolution resolution, DateTime from, DateTime toExclusive, CancellationToken cancellationToken) =>
        await dbContext.ProbeRollups
            .AsNoTracking()
            .Where(rollup => rollup.Resolution == resolution
                && (targetId == null || rollup.TargetId == targetId)
                && rollup.BucketStart >= from
                && rollup.BucketStart < toExclusive)
            .OrderBy(rollup => rollup.BucketStart)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyDictionary<int, DateTime>> GetLatestRollupBucketsAsync(
        ProbeResolution resolution, CancellationToken cancellationToken)
    {
        var targetIds = await dbContext.ProbeRollups
            .Where(rollup => rollup.Resolution == resolution)
            .Select(rollup => rollup.TargetId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var latest = new Dictionary<int, DateTime>();
        foreach (var targetId in targetIds)
        {
            var bucket = await dbContext.ProbeRollups
                .Where(rollup => rollup.Resolution == resolution && rollup.TargetId == targetId)
                .OrderByDescending(rollup => rollup.BucketStart)
                .Select(rollup => new { rollup.BucketStart })
                .FirstAsync(cancellationToken);
            latest[targetId] = bucket.BucketStart;
        }

        return latest;
    }

    public Task<int> DeleteSamplesOlderThanAsync(DateTime cutoff, CancellationToken cancellationToken) =>
        dbContext.ProbeSamples.Where(sample => sample.Timestamp < cutoff).ExecuteDeleteAsync(cancellationToken);

    public Task<int> DeleteRollupsOlderThanAsync(ProbeResolution resolution, DateTime cutoff, CancellationToken cancellationToken) =>
        dbContext.ProbeRollups
            .Where(rollup => rollup.Resolution == resolution && rollup.BucketStart < cutoff)
            .ExecuteDeleteAsync(cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => dbContext.SaveChangesAsync(cancellationToken);
}
