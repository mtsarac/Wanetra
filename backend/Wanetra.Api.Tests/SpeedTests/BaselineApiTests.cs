using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Wanetra.Domain;

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
