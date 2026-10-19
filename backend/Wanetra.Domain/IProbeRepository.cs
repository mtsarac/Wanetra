namespace Wanetra.Domain;

public interface IProbeRepository
{
    Task<IReadOnlyList<ProbeTarget>> ListTargetsAsync(bool onlyEnabled, CancellationToken cancellationToken);
    Task<ProbeTarget?> GetTargetAsync(int id, CancellationToken cancellationToken);
    Task<int> CountTargetsAsync(CancellationToken cancellationToken);
    void AddTarget(ProbeTarget target);
    void RemoveTarget(ProbeTarget target);

    void AddSamples(IEnumerable<ProbeSample> samples);
    Task<IReadOnlyList<ProbeSample>> GetSamplesAsync(int targetId, DateTime from, DateTime to, CancellationToken cancellationToken);
    Task<IReadOnlyList<ProbeSample>> GetSamplesSinceAsync(DateTime since, CancellationToken cancellationToken);
    Task<IReadOnlyList<ProbeSample>> GetSamplesBetweenAsync(DateTime from, DateTime toExclusive, CancellationToken cancellationToken);
    Task<DateTime?> GetEarliestSampleAsync(CancellationToken cancellationToken);

    void AddRollups(IEnumerable<ProbeRollup> rollups);
    Task<IReadOnlyList<ProbeRollup>> GetRollupsAsync(
        int? targetId, ProbeResolution resolution, DateTime from, DateTime toExclusive, CancellationToken cancellationToken);
    Task<IReadOnlyDictionary<int, DateTime>> GetLatestRollupBucketsAsync(ProbeResolution resolution, CancellationToken cancellationToken);

    Task<int> DeleteSamplesOlderThanAsync(DateTime cutoff, CancellationToken cancellationToken);
    Task<int> DeleteRollupsOlderThanAsync(ProbeResolution resolution, DateTime cutoff, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
