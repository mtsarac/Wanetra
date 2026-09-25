using Wanetra.Application.Baselines;
using Wanetra.Domain;

namespace Wanetra.Api.Tests.SpeedTests;

public class BaselineServiceTests
{
    [Fact]
    public async Task Calculates_metric_baselines_from_successful_results_in_utc_window()
    {
        var now = new DateTimeOffset(2026, 9, 21, 12, 0, 0, TimeSpan.Zero);
        var repository = new RecordingResultRepository
        {
            Results =
            {
                Result(now.AddDays(-8), download: 1, upload: 1),
                Result(now.AddHours(-4), download: 1, upload: 1, success: false),
                Result(now.AddHours(-3), download: 999, upload: 999),
                Result(now.AddHours(-2), download: 20, upload: 2),
                Result(now.AddHours(-2), download: 30, upload: 3),
                Result(now.AddHours(-2), download: 40, upload: 4),
                Result(now.AddHours(-2), download: 50, upload: 5),
                Result(now.AddHours(-2), download: 60, upload: 6),
                Result(now.AddHours(-2), download: 70, upload: 7),
                Result(now.AddHours(-2), download: 80, upload: 8),
                Result(now.AddHours(-2), download: 90, upload: 9),
                Result(now.AddHours(-1), download: 100, upload: 10),
                Result(now.AddMinutes(-30), download: 110, upload: null),
                Result(now.AddMinutes(-5), download: 120, upload: 12),
            },
        };
        var service = new BaselineService(repository, new FakeTimeProvider(now));

        var baseline = await service.GetAsync(CancellationToken.None);

        Assert.Equal(now.AddDays(-7).UtcDateTime, baseline.WindowFrom);
        Assert.Equal(now.UtcDateTime, baseline.WindowTo);
        Assert.Equal(12, baseline.Download.ValidSamples);
        Assert.True(baseline.Download.Available);
        Assert.Equal(75, baseline.Download.BaselineMbps);
        Assert.Equal(120, baseline.Download.LatestMbps);
        Assert.Equal(60, baseline.Download.PercentChange);
        Assert.Equal(11, baseline.Upload.ValidSamples);
        Assert.True(baseline.Upload.Available);
        Assert.Equal(7, baseline.Upload.BaselineMbps);
        Assert.Equal(12, baseline.Upload.LatestMbps);
        Assert.NotNull(baseline.Upload.PercentChange);
        Assert.Equal(71.42857142857143, baseline.Upload.PercentChange.Value, 10);
    }

    [Fact]
    public async Task Measurement_baseline_excludes_results_at_the_measurement_timestamp()
    {
        var now = new DateTimeOffset(2026, 9, 21, 12, 0, 0, TimeSpan.Zero);
        var repository = new RecordingResultRepository
        {
            Results = Enumerable.Range(1, 10)
                .Select(index => Result(now.AddMinutes(-index), download: index * 10, upload: index))
                .Append(Result(now, download: 10000, upload: 10000))
                .ToList(),
        };
        var service = new BaselineService(repository, new FakeTimeProvider(now));

        var baseline = await service.GetForMeasurementAsync(now.UtcDateTime, CancellationToken.None);

        Assert.Equal(10, baseline.Download.ValidSamples);
        Assert.Equal(55, baseline.Download.BaselineMbps);
        Assert.Equal(100, baseline.Download.LatestMbps);
        Assert.Equal(10, baseline.Upload.ValidSamples);
        Assert.Equal(5.5, baseline.Upload.BaselineMbps);
        Assert.Equal(10, baseline.Upload.LatestMbps);
    }

    [Fact]
    public async Task Leaves_metric_unavailable_until_it_has_ten_valid_samples()
    {
        var now = new DateTimeOffset(2026, 9, 21, 12, 0, 0, TimeSpan.Zero);
        var repository = new RecordingResultRepository();
        for (var index = 1; index <= 10; index++)
        {
            repository.Results.Add(Result(now.AddMinutes(-index), download: index, upload: index == 10 ? 10 : null));
        }
        var service = new BaselineService(repository, new FakeTimeProvider(now));

        var baseline = await service.GetAsync(CancellationToken.None);

        Assert.True(baseline.Download.Available);
        Assert.False(baseline.Upload.Available);
        Assert.Null(baseline.Upload.BaselineMbps);
        Assert.Null(baseline.Upload.PercentChange);
        Assert.Equal(1, baseline.Upload.ValidSamples);
    }

    private static SpeedTestResult Result(
        DateTimeOffset timestamp,
        double? download,
        double? upload,
        bool success = true) => new()
        {
            Engine = "test",
            Timestamp = timestamp.UtcDateTime,
            Success = success,
            DownloadMbps = download,
            UploadMbps = upload,
        };

    private sealed class FakeTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
