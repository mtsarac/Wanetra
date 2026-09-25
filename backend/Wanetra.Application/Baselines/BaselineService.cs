using Wanetra.Domain;

namespace Wanetra.Application.Baselines;

public sealed record MetricBaseline(
    bool Available,
    int ValidSamples,
    double? BaselineMbps,
    double? LatestMbps,
    double? PercentChange);

public sealed record BaselineSnapshot(
    DateTime WindowFrom,
    DateTime WindowTo,
    MetricBaseline Download,
    MetricBaseline Upload);

public sealed class BaselineService(
    ISpeedTestResultRepository repository,
    TimeProvider timeProvider)
{
    private const int MinimumSamples = 10;
    private static readonly TimeSpan Window = TimeSpan.FromDays(7);

    public Task<BaselineSnapshot> GetAsync(CancellationToken cancellationToken) =>
        GetAsync(timeProvider.GetUtcNow().UtcDateTime, includeUpperBound: true, cancellationToken);

    public Task<BaselineSnapshot> GetForMeasurementAsync(DateTime timestamp, CancellationToken cancellationToken) =>
        GetAsync(timestamp, includeUpperBound: false, cancellationToken);

    private async Task<BaselineSnapshot> GetAsync(
        DateTime windowTo,
        bool includeUpperBound,
        CancellationToken cancellationToken)
    {
        var windowFrom = windowTo - Window;
        var results = includeUpperBound
            ? await repository.FindSuccessfulSinceAsync(windowFrom, windowTo, cancellationToken)
            : await repository.FindSuccessfulBeforeAsync(windowFrom, windowTo, cancellationToken);
        var latest = includeUpperBound
            ? await repository.FindLatestSuccessfulAsync(cancellationToken)
            : await repository.FindLatestSuccessfulBeforeAsync(windowTo, cancellationToken);

        return new BaselineSnapshot(
            windowFrom,
            windowTo,
            Calculate(results.Select(result => result.DownloadMbps), latest?.DownloadMbps),
            Calculate(results.Select(result => result.UploadMbps), latest?.UploadMbps));
    }

    private static MetricBaseline Calculate(IEnumerable<double?> values, double? latest)
    {
        var samples = values.Where(value => value.HasValue).Select(value => value!.Value).Order().ToArray();
        if (samples.Length < MinimumSamples)
        {
            return new MetricBaseline(false, samples.Length, null, latest, null);
        }

        var midpoint = samples.Length / 2;
        var median = samples.Length % 2 == 0
            ? (samples[midpoint - 1] + samples[midpoint]) / 2
            : samples[midpoint];
        double? percentChange = latest.HasValue && median != 0
            ? (latest.Value - median) / median * 100
            : null;

        return new MetricBaseline(true, samples.Length, median, latest, percentChange);
    }
}
