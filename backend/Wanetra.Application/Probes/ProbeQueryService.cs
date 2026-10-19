using System.Globalization;
using Wanetra.Application.Settings;
using Wanetra.Domain;

namespace Wanetra.Application.Probes;

public enum SeriesResolution
{
    Auto,
    Raw,
    Minute,
    Hour,
}

public sealed record ProbePoint(
    DateTime Timestamp,
    double? RttMs,
    double? RttMinMs,
    double? RttMaxMs,
    double? JitterMs,
    double LossPercent,
    int Sent);

public sealed record ProbeSeries(int TargetId, SeriesResolution Resolution, DateTime From, DateTime To, IReadOnlyList<ProbePoint> Points);

public sealed record ProbeTargetStatus(
    ProbeTarget Target,
    bool? Up,
    double? RttMs,
    DateTime? CheckedAt,
    double? LossPercent,
    double? AvgRttMs,
    string? Error);

public sealed record ConnectivityStatus(
    bool Monitoring,
    int IntervalSeconds,
    string State,
    Diagnosis Diagnosis,
    DateTime? LastCheckedAt,
    IReadOnlyList<ProbeTargetStatus> Targets,
    Outage? ActiveOutage);

public sealed record UptimeSummary(
    DateTime From,
    DateTime To,
    double UptimePercent,
    int OutageCount,
    double TotalOutageSeconds,
    double LongestOutageSeconds,
    bool CoversFullRange);

public sealed class SeriesException(string message) : Exception(message);

public sealed class ProbeQueryService(
    IProbeRepository probes,
    IOutageRepository outages,
    IAppSettingRepository appSettings,
    SettingsSnapshot settings,
    TimeProvider timeProvider)
{
    private static readonly TimeSpan StatusWindow = TimeSpan.FromMinutes(5);
    private const int MaxRawRangeHours = 24;

    public async Task<ProbeSeries> GetSeriesAsync(
        int targetId, DateTime from, DateTime to, SeriesResolution requested, CancellationToken cancellationToken)
    {
        if (to <= from)
        {
            throw new SeriesException("'to' must be after 'from'.");
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var resolution = Resolve(requested, from, to, now);
        var points = resolution switch
        {
            SeriesResolution.Raw => (await probes.GetSamplesAsync(targetId, from, to, cancellationToken))
                .Select(sample => new ProbePoint(
                    sample.Timestamp, sample.RttMs, sample.RttMs, sample.RttMs, null, sample.Success ? 0 : 100, 1))
                .ToList(),
            _ => (await probes.GetRollupsAsync(
                    targetId,
                    resolution == SeriesResolution.Hour ? ProbeResolution.Hour : ProbeResolution.Minute,
                    from,
                    to,
                    cancellationToken))
                .Select(rollup => new ProbePoint(
                    rollup.BucketStart, rollup.RttAvgMs, rollup.RttMinMs, rollup.RttMaxMs, rollup.JitterMs, rollup.LossPercent, rollup.Sent))
                .ToList(),
        };

        return new ProbeSeries(targetId, resolution, from, to, points);
    }

    public async Task<ConnectivityStatus> GetStatusAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var interval = settings.GetInt(SettingKeys.ProbesIntervalSeconds);
        var monitoring = settings.GetBool(SettingKeys.ProbesEnabled);
        var targets = await probes.ListTargetsAsync(onlyEnabled: false, cancellationToken);
        var samples = await probes.GetSamplesSinceAsync(now - StatusWindow, cancellationToken);
        var open = await outages.GetOpenOutageAsync(cancellationToken);

        var freshLimit = now - TimeSpan.FromSeconds(Math.Max(interval * 3, 30));
        var statuses = new List<ProbeTargetStatus>();
        var latestForDiagnosis = new List<(ProbeRole Role, bool Success)>();
        DateTime? lastChecked = null;

        foreach (var target in targets)
        {
            var own = samples.Where(sample => sample.TargetId == target.Id).ToList();
            var latest = own.LastOrDefault();
            if (latest is null)
            {
                statuses.Add(new ProbeTargetStatus(target, null, null, null, null, null, null));
                continue;
            }

            var received = own.Where(sample => sample.Success && sample.RttMs.HasValue).Select(sample => sample.RttMs!.Value).ToList();
            statuses.Add(new ProbeTargetStatus(
                target,
                latest.Success,
                latest.RttMs,
                latest.Timestamp,
                own.Count(sample => !sample.Success) * 100.0 / own.Count,
                received.Count > 0 ? received.Average() : null,
                latest.Error));
            if (lastChecked is null || latest.Timestamp > lastChecked)
            {
                lastChecked = latest.Timestamp;
            }

            if (target.Enabled && latest.Timestamp >= freshLimit)
            {
                latestForDiagnosis.Add((target.Role, latest.Success));
            }
        }

        var diagnosis = latestForDiagnosis.Count > 0 ? OutageClassifier.Classify(latestForDiagnosis) : Diagnosis.Healthy;
        var state = !monitoring ? "disabled"
            : open is not null ? "outage"
            : latestForDiagnosis.Count == 0 ? "unknown"
            : diagnosis.Scope is not null ? "degraded"
            : "healthy";

        return new ConnectivityStatus(monitoring, interval, state, diagnosis, lastChecked, statuses, open);
    }

    public async Task<UptimeSummary> GetUptimeAsync(TimeSpan window, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var from = now - window;

        var stored = await appSettings.ListAsync(cancellationToken);
        var since = stored.FirstOrDefault(setting => setting.Key == ProbeInternalKeys.MonitoringSince)?.Value;
        var coversFullRange = true;
        if (since is not null
            && DateTime.TryParse(since, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var monitoringSince)
            && monitoringSince > from)
        {
            from = monitoringSince;
            coversFullRange = false;
        }

        var overlapping = await outages.GetOutagesOverlappingAsync(from, now, cancellationToken);
        var durations = overlapping
            .Select(outage => (End: outage.EndedAt ?? now, Start: outage.StartedAt))
            .Select(span => (Start: span.Start < from ? from : span.Start, End: span.End > now ? now : span.End))
            .Where(span => span.End > span.Start)
            .Select(span => (span.End - span.Start).TotalSeconds)
            .ToList();

        var total = Math.Max((now - from).TotalSeconds, 1);
        var down = durations.Sum();
        return new UptimeSummary(
            from,
            now,
            Math.Clamp(100 * (1 - down / total), 0, 100),
            durations.Count,
            down,
            durations.Count > 0 ? durations.Max() : 0,
            coversFullRange);
    }

    private SeriesResolution Resolve(SeriesResolution requested, DateTime from, DateTime to, DateTime now)
    {
        var range = to - from;
        var rawCovered = from >= now.AddHours(-settings.GetInt(SettingKeys.ProbesRawRetentionHours));
        var minuteCovered = from >= now.AddDays(-settings.GetInt(SettingKeys.ProbesMinuteRetentionDays));

        if (requested == SeriesResolution.Raw && range.TotalHours > MaxRawRangeHours)
        {
            throw new SeriesException($"Raw samples can cover at most {MaxRawRangeHours} hours.");
        }

        if (requested != SeriesResolution.Auto)
        {
            return requested;
        }

        if (range <= TimeSpan.FromHours(3) && rawCovered)
        {
            return SeriesResolution.Raw;
        }

        return range <= TimeSpan.FromHours(48) && minuteCovered ? SeriesResolution.Minute : SeriesResolution.Hour;
    }
}
