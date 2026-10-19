using Microsoft.Extensions.DependencyInjection;
using Wanetra.Application.Probes;
using Wanetra.Domain;

namespace Wanetra.Api.Tests.Probes;

public class ProbeRollupTests
{
    private static readonly DateTime Hour11 = new(2026, 10, 18, 11, 0, 0, DateTimeKind.Utc);

    private static ProbeSample Sample(int targetId, DateTime at, double? rtt, bool success = true) => new()
    {
        TargetId = targetId,
        Timestamp = at,
        Success = success,
        RttMs = success ? rtt : null,
    };

    private static async Task SeedAsync(ProbeApiFactory factory, params ProbeSample[] samples)
    {
        await factory.WithScopeAsync(async services =>
        {
            var repository = services.GetRequiredService<IProbeRepository>();
            repository.AddSamples(samples);
            await repository.SaveChangesAsync(CancellationToken.None);
            return 0;
        });
    }

    private static Task<IReadOnlyList<ProbeRollup>> RollupsAsync(ProbeApiFactory factory, ProbeResolution resolution) =>
        factory.WithScopeAsync(services => services.GetRequiredService<IProbeRepository>()
            .GetRollupsAsync(null, resolution, DateTime.MinValue, DateTime.MaxValue, CancellationToken.None));

    private static Task RollUpAsync(ProbeApiFactory factory) =>
        factory.WithScopeAsync(async services =>
        {
            await services.GetRequiredService<ProbeRollupService>().RollUpAsync(CancellationToken.None);
            return 0;
        });

    [Fact]
    public void A_minute_summarizes_loss_latency_and_jitter()
    {
        var samples = new[]
        {
            Sample(1, Hour11, 10),
            Sample(1, Hour11.AddSeconds(10), 20),
            Sample(1, Hour11.AddSeconds(20), null, success: false),
            Sample(1, Hour11.AddSeconds(30), 10),
        };

        var rollup = ProbeRollupService.FromSamples(1, Hour11, samples);

        Assert.Equal(4, rollup.Sent);
        Assert.Equal(3, rollup.Received);
        Assert.Equal(25, rollup.LossPercent);
        Assert.Equal(10, rollup.RttMinMs);
        Assert.Equal(20, rollup.RttMaxMs);
        Assert.Equal(40.0 / 3, rollup.RttAvgMs!.Value, precision: 6);
        Assert.Equal(10, rollup.JitterMs);
    }

    [Fact]
    public void A_minute_without_any_reply_has_no_latency()
    {
        var rollup = ProbeRollupService.FromSamples(1, Hour11, [Sample(1, Hour11, null, success: false)]);

        Assert.Equal(100, rollup.LossPercent);
        Assert.Null(rollup.RttAvgMs);
        Assert.Null(rollup.JitterMs);
    }

    [Fact]
    public void An_hour_weights_each_minute_by_its_replies()
    {
        var minutes = new[]
        {
            new ProbeRollup { Sent = 6, Received = 6, RttMinMs = 5, RttAvgMs = 10, RttMaxMs = 20, JitterMs = 2 },
            new ProbeRollup { Sent = 6, Received = 2, RttMinMs = 30, RttAvgMs = 40, RttMaxMs = 50, JitterMs = 8 },
            new ProbeRollup { Sent = 6, Received = 0 },
        };

        var hour = ProbeRollupService.Combine(1, Hour11, minutes);

        Assert.Equal(18, hour.Sent);
        Assert.Equal(8, hour.Received);
        Assert.Equal(5, hour.RttMinMs);
        Assert.Equal(50, hour.RttMaxMs);
        Assert.Equal((10 * 6 + 40 * 2) / 8.0, hour.RttAvgMs);
        Assert.Equal((2 * 6 + 8 * 2) / 8.0, hour.JitterMs);
    }

    [Fact]
    public async Task Completed_minutes_become_rollups_once_and_the_open_minute_waits()
    {
        await using var factory = new ProbeApiFactory();
        _ = factory.CreateClient();
        await SeedAsync(factory,
            Sample(2, Hour11, 10),
            Sample(2, Hour11.AddSeconds(10), 20),
            Sample(2, Hour11.AddSeconds(70), 30),
            Sample(3, Hour11.AddSeconds(5), 7, success: false));

        await RollUpAsync(factory);
        await RollUpAsync(factory);

        var minutes = await RollupsAsync(factory, ProbeResolution.Minute);
        Assert.Equal(3, minutes.Count);
        var first = minutes.Single(rollup => rollup.TargetId == 2 && rollup.BucketStart == Hour11);
        Assert.Equal(2, first.Sent);
        Assert.Equal(15, first.RttAvgMs);
        Assert.Equal(100, minutes.Single(rollup => rollup.TargetId == 3).LossPercent);
    }

    [Fact]
    public async Task The_current_minute_is_not_rolled_up_early()
    {
        await using var factory = new ProbeApiFactory();
        _ = factory.CreateClient();
        var now = factory.Time.GetUtcNow().UtcDateTime;
        await SeedAsync(factory, Sample(2, now.AddSeconds(-5), 10));

        await RollUpAsync(factory);

        Assert.Empty(await RollupsAsync(factory, ProbeResolution.Minute));
    }

    [Fact]
    public async Task Completed_hours_are_built_from_the_minute_rollups()
    {
        await using var factory = new ProbeApiFactory();
        _ = factory.CreateClient();
        var samples = Enumerable.Range(0, 60)
            .Select(minute => Sample(2, Hour11.AddMinutes(minute), minute % 2 == 0 ? 10 : 20))
            .ToList();
        samples[5] = Sample(2, samples[5].Timestamp, null, success: false);
        await SeedAsync(factory, [.. samples]);

        // The hour ends at 12:00; hours are only closed once the minute rollups for them are surely in.
        await RollUpAsync(factory);
        Assert.Empty(await RollupsAsync(factory, ProbeResolution.Hour));
        factory.Time.Advance(TimeSpan.FromMinutes(3));
        await RollUpAsync(factory);

        var hours = await RollupsAsync(factory, ProbeResolution.Hour);
        var hour = Assert.Single(hours);
        Assert.Equal(Hour11, hour.BucketStart);
        Assert.Equal(60, hour.Sent);
        Assert.Equal(59, hour.Received);
        Assert.Equal(10, hour.RttMinMs);
        Assert.Equal(20, hour.RttMaxMs);
    }

    [Fact]
    public async Task Retention_removes_old_samples_and_rollups_but_keeps_recent_ones()
    {
        await using var factory = new ProbeApiFactory();
        _ = factory.CreateClient();
        var now = factory.Time.GetUtcNow().UtcDateTime;
        await SeedAsync(factory,
            Sample(2, now.AddHours(-49), 10),
            Sample(2, now.AddHours(-1), 10));
        await factory.WithScopeAsync(async services =>
        {
            var repository = services.GetRequiredService<IProbeRepository>();
            repository.AddRollups(
            [
                new ProbeRollup { TargetId = 2, Resolution = ProbeResolution.Minute, BucketStart = now.AddDays(-31), Sent = 1, Received = 1 },
                new ProbeRollup { TargetId = 2, Resolution = ProbeResolution.Minute, BucketStart = now.AddDays(-2), Sent = 1, Received = 1 },
                new ProbeRollup { TargetId = 2, Resolution = ProbeResolution.Hour, BucketStart = now.AddDays(-366), Sent = 1, Received = 1 },
                new ProbeRollup { TargetId = 2, Resolution = ProbeResolution.Hour, BucketStart = now.AddDays(-100), Sent = 1, Received = 1 },
            ]);
            await repository.SaveChangesAsync(CancellationToken.None);
            return 0;
        });

        await factory.WithScopeAsync(async services =>
        {
            await services.GetRequiredService<ProbeRollupService>().ApplyRetentionAsync(CancellationToken.None);
            return 0;
        });

        var samples = await factory.WithScopeAsync(services => services.GetRequiredService<IProbeRepository>()
            .GetSamplesSinceAsync(DateTime.MinValue, CancellationToken.None));
        Assert.Equal(now.AddHours(-1), Assert.Single(samples).Timestamp);
        Assert.Equal(now.AddDays(-2), Assert.Single(await RollupsAsync(factory, ProbeResolution.Minute)).BucketStart);
        Assert.Equal(now.AddDays(-100), Assert.Single(await RollupsAsync(factory, ProbeResolution.Hour)).BucketStart);
    }

    [Fact]
    public async Task Deleting_a_target_removes_its_samples_and_rollups()
    {
        await using var factory = new ProbeApiFactory();
        var client = factory.CreateClient();
        await SeedAsync(factory, Sample(2, Hour11, 10), Sample(3, Hour11, 10));
        factory.Time.Advance(TimeSpan.FromHours(2));
        await RollUpAsync(factory);

        var response = await client.DeleteAsync("/api/probes/targets/2");
        Assert.Equal(System.Net.HttpStatusCode.NoContent, response.StatusCode);

        var samples = await factory.WithScopeAsync(services => services.GetRequiredService<IProbeRepository>()
            .GetSamplesSinceAsync(DateTime.MinValue, CancellationToken.None));
        Assert.All(samples, sample => Assert.Equal(3, sample.TargetId));
        Assert.All(await RollupsAsync(factory, ProbeResolution.Minute), rollup => Assert.Equal(3, rollup.TargetId));
    }
}
