using Wanetra.Application.Settings;
using Wanetra.Domain;

namespace Wanetra.Application.Probes;

public sealed class ProbeRollupService(
    IProbeRepository probes,
    IOutageRepository outages,
    SettingsSnapshot settings,
    TimeProvider timeProvider)
{
    private static readonly TimeSpan SettleTime = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan Chunk = TimeSpan.FromHours(6);

    public async Task RollUpAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        await RollUpMinutesAsync(now, cancellationToken);
        await RollUpHoursAsync(now, cancellationToken);
    }

    public async Task ApplyRetentionAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var rawCutoff = now.AddHours(-settings.GetInt(SettingKeys.ProbesRawRetentionHours));
        var minuteCutoff = now.AddDays(-settings.GetInt(SettingKeys.ProbesMinuteRetentionDays));
        var longCutoff = now.AddDays(-settings.GetInt(SettingKeys.RetentionDays));

        await probes.DeleteSamplesOlderThanAsync(rawCutoff, cancellationToken);
        await probes.DeleteRollupsOlderThanAsync(ProbeResolution.Minute, minuteCutoff, cancellationToken);
        await probes.DeleteRollupsOlderThanAsync(ProbeResolution.Hour, longCutoff, cancellationToken);
        await outages.DeleteOutagesOlderThanAsync(longCutoff, cancellationToken);
    }

    private async Task RollUpMinutesAsync(DateTime now, CancellationToken cancellationToken)
    {
        var end = Floor(now - SettleTime, TimeSpan.FromMinutes(1));
        var watermarks = await probes.GetLatestRollupBucketsAsync(ProbeResolution.Minute, cancellationToken);
        var rawWindowStart = now.AddHours(-settings.GetInt(SettingKeys.ProbesRawRetentionHours));

        DateTime start;
        if (watermarks.Count > 0)
        {
            start = watermarks.Values.Min().AddMinutes(1);
        }
        else
        {
            var earliest = await probes.GetEarliestSampleAsync(cancellationToken);
            if (earliest is null)
            {
                return;
            }

            start = Floor(earliest.Value, TimeSpan.FromMinutes(1));
        }

        if (start < rawWindowStart)
        {
            start = Floor(rawWindowStart, TimeSpan.FromMinutes(1));
        }

        for (var from = start; from < end; from += Chunk)
        {
            var to = from + Chunk < end ? from + Chunk : end;
            var samples = await probes.GetSamplesBetweenAsync(from, to, cancellationToken);
            var rollups = samples
                .GroupBy(sample => (sample.TargetId, Bucket: Floor(sample.Timestamp, TimeSpan.FromMinutes(1))))
                .Where(group => !watermarks.TryGetValue(group.Key.TargetId, out var latest) || group.Key.Bucket > latest)
                .Select(group => FromSamples(group.Key.TargetId, group.Key.Bucket, group.OrderBy(sample => sample.Timestamp).ToList()))
                .ToList();
            if (rollups.Count == 0)
            {
                continue;
            }

            probes.AddRollups(rollups);
            await probes.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task RollUpHoursAsync(DateTime now, CancellationToken cancellationToken)
    {
        var end = Floor(now - TimeSpan.FromMinutes(2), TimeSpan.FromHours(1));
        var watermarks = await probes.GetLatestRollupBucketsAsync(ProbeResolution.Hour, cancellationToken);
        var minuteWindowStart = now.AddDays(-settings.GetInt(SettingKeys.ProbesMinuteRetentionDays));

        DateTime start;
        if (watermarks.Count > 0)
        {
            start = watermarks.Values.Min().AddHours(1);
        }
        else
        {
            var minutes = await probes.GetRollupsAsync(null, ProbeResolution.Minute, DateTime.MinValue, end, cancellationToken);
            if (minutes.Count == 0)
            {
                return;
            }

            start = Floor(minutes.Min(rollup => rollup.BucketStart), TimeSpan.FromHours(1));
        }

        if (start < minuteWindowStart)
        {
            start = Floor(minuteWindowStart, TimeSpan.FromHours(1));
        }

        for (var from = start; from < end; from += TimeSpan.FromDays(1))
        {
            var to = from + TimeSpan.FromDays(1) < end ? from + TimeSpan.FromDays(1) : end;
            var minutes = await probes.GetRollupsAsync(null, ProbeResolution.Minute, from, to, cancellationToken);
            var rollups = minutes
                .GroupBy(rollup => (rollup.TargetId, Bucket: Floor(rollup.BucketStart, TimeSpan.FromHours(1))))
                .Where(group => !watermarks.TryGetValue(group.Key.TargetId, out var latest) || group.Key.Bucket > latest)
                .Select(group => Combine(group.Key.TargetId, group.Key.Bucket, group.ToList()))
                .ToList();
            if (rollups.Count == 0)
            {
                continue;
            }

            probes.AddRollups(rollups);
            await probes.SaveChangesAsync(cancellationToken);
        }
    }

    public static ProbeRollup FromSamples(int targetId, DateTime bucket, IReadOnlyList<ProbeSample> samples)
    {
        var rtts = samples.Where(sample => sample.Success && sample.RttMs.HasValue).Select(sample => sample.RttMs!.Value).ToList();
        double? jitter = null;
        if (rtts.Count > 1)
        {
            jitter = rtts.Zip(rtts.Skip(1), (previous, next) => Math.Abs(next - previous)).Average();
        }

        return new ProbeRollup
        {
            TargetId = targetId,
            Resolution = ProbeResolution.Minute,
            BucketStart = bucket,
            Sent = samples.Count,
            Received = samples.Count(sample => sample.Success),
            RttMinMs = rtts.Count > 0 ? rtts.Min() : null,
            RttAvgMs = rtts.Count > 0 ? rtts.Average() : null,
            RttMaxMs = rtts.Count > 0 ? rtts.Max() : null,
            JitterMs = jitter,
        };
    }

    public static ProbeRollup Combine(int targetId, DateTime bucket, IReadOnlyList<ProbeRollup> minutes)
    {
        var withRtt = minutes.Where(minute => minute.RttAvgMs.HasValue && minute.Received > 0).ToList();
        var weight = withRtt.Sum(minute => minute.Received);
        var withJitter = withRtt.Where(minute => minute.JitterMs.HasValue).ToList();
        var jitterWeight = withJitter.Sum(minute => minute.Received);

        return new ProbeRollup
        {
            TargetId = targetId,
            Resolution = ProbeResolution.Hour,
            BucketStart = bucket,
            Sent = minutes.Sum(minute => minute.Sent),
            Received = minutes.Sum(minute => minute.Received),
            RttMinMs = withRtt.Count > 0 ? withRtt.Min(minute => minute.RttMinMs) : null,
            RttAvgMs = weight > 0 ? withRtt.Sum(minute => minute.RttAvgMs!.Value * minute.Received) / weight : null,
            RttMaxMs = withRtt.Count > 0 ? withRtt.Max(minute => minute.RttMaxMs) : null,
            JitterMs = jitterWeight > 0 ? withJitter.Sum(minute => minute.JitterMs!.Value * minute.Received) / jitterWeight : null,
        };
    }

    public static DateTime Floor(DateTime value, TimeSpan size) =>
        new(value.Ticks - value.Ticks % size.Ticks, DateTimeKind.Utc);
}
