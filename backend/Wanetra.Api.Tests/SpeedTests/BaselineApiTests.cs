using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Wanetra.Domain;
using Wanetra.Application.Baselines;
using Wanetra.Infrastructure.Persistence;

namespace Wanetra.Api.Tests.SpeedTests;

public class BaselineApiTests
{
    [Fact]
    public async Task Get_baseline_returns_current_baseline_snapshot()
    {
        await using var factory = new BaselineApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/baseline");
        var baseline = await response.Content.ReadFromJsonAsync<BaselineResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(baseline);
        Assert.True(baseline.Download.Available);
        Assert.Equal(60, baseline.Download.BaselineMbps);
        Assert.Equal(120, baseline.Download.LatestMbps);
        Assert.False(baseline.Upload.Available);
        Assert.Null(baseline.Upload.PercentChange);
    }

    [Fact]
    public async Task Measurement_baseline_excludes_the_measurement_at_its_cutoff_in_sqlite()
    {
        await using var factory = new WanetraApiFactory();
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live")).StatusCode);
        var timestamp = new DateTime(2026, 9, 21, 12, 0, 0, DateTimeKind.Utc);

        await using var scope = factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<WanetraDbContext>();
        database.SpeedTestResults.AddRange(
            Enumerable.Range(1, 10).Select(index => new SpeedTestResult
            {
                Engine = "test",
                Success = true,
                Timestamp = timestamp.AddMinutes(-index),
                DownloadMbps = index * 10,
                UploadMbps = index,
            }).Append(new SpeedTestResult
            {
                Engine = "test",
                Success = true,
                Timestamp = timestamp,
                DownloadMbps = 1000,
                UploadMbps = 1000,
            }));
        await database.SaveChangesAsync();

        var service = scope.ServiceProvider.GetRequiredService<BaselineService>();
        var baseline = await service.GetForMeasurementAsync(timestamp, CancellationToken.None);

        Assert.Equal(10, baseline.Download.ValidSamples);
        Assert.Equal(55, baseline.Download.BaselineMbps);
        Assert.Equal(10, baseline.Download.LatestMbps);
        Assert.Equal(10, baseline.Upload.ValidSamples);
        Assert.Equal(5.5, baseline.Upload.BaselineMbps);
        Assert.Equal(1, baseline.Upload.LatestMbps);
    }

    private sealed class BaselineApiFactory : WanetraApiFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ISpeedTestResultRepository>();
                services.RemoveAll<TimeProvider>();
                var now = new DateTimeOffset(2026, 9, 21, 12, 0, 0, TimeSpan.Zero);
                services.AddSingleton<TimeProvider>(new FakeTimeProvider(now));
                services.AddScoped<ISpeedTestResultRepository>(_ => new RecordingResultRepository
                {
                    Results = Enumerable.Range(1, 10)
                        .Select(index => new SpeedTestResult
                        {
                            Engine = "test",
                            Success = true,
                            Timestamp = now.AddMinutes(-index).UtcDateTime,
                            DownloadMbps = index * 10,
                            UploadMbps = index == 1 ? 12 : null,
                        })
                        .Append(new SpeedTestResult
                        {
                            Engine = "test",
                            Success = true,
                            Timestamp = now.AddMinutes(-1).UtcDateTime,
                            DownloadMbps = 120,
                            UploadMbps = null,
                        })
                        .ToList(),
                });
            });
        }
    }

    private sealed class FakeTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed record BaselineResponse(MetricBaselineResponse Download, MetricBaselineResponse Upload);

    private sealed record MetricBaselineResponse(
        bool Available,
        int ValidSamples,
        double? BaselineMbps,
        double? LatestMbps,
        double? PercentChange);
}
