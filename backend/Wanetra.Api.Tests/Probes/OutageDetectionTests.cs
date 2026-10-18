using Microsoft.Extensions.DependencyInjection;
using Wanetra.Application.Notifications;
using Wanetra.Application.Probes;
using Wanetra.Application.SpeedTests;
using Wanetra.Domain;

namespace Wanetra.Api.Tests.Probes;

public class OutageDetectionTests
{
    private static readonly DateTime Noon = new(2026, 10, 18, 12, 0, 0, DateTimeKind.Utc);

    private static bool IsInternet(ProbeRequest request) =>
        request.Kind != ProbeKind.Dns && request.Host != ProbeApiFactory.Gateway;

    [Fact]
    public async Task Default_targets_are_probed_each_cycle_including_the_detected_gateway()
    {
        await using var factory = new ProbeApiFactory();
        _ = factory.CreateClient();

        var result = await factory.CycleAsync();

        Assert.Equal(5, result.Results.Count);
        var hosts = factory.Executor.Requests.Select(request => request.Host).ToList();
        Assert.Contains(ProbeApiFactory.Gateway, hosts);
        Assert.Contains("1.1.1.1", hosts);
        Assert.Contains("cloudflare.com", hosts);
        Assert.All(result.Results, probe => Assert.True(probe.Outcome.Success));
    }

    [Fact]
    public async Task A_gateway_target_is_skipped_when_no_default_route_exists()
    {
        await using var factory = new ProbeApiFactory();
        _ = factory.CreateClient();
        factory.GatewayDetector.Address = null;

        var result = await factory.CycleAsync();

        Assert.Equal(4, result.Results.Count);
        Assert.DoesNotContain(result.Results, probe => probe.Target.Role == ProbeRole.Gateway);
    }

    [Fact]
    public async Task Consecutive_internet_failures_open_one_outage_started_at_the_first_failure_and_notify()
    {
        await using var factory = new ProbeApiFactory();
        _ = factory.CreateClient();
        await factory.AddWebhookDestinationAsync();
        factory.Executor.Failing(IsInternet);

        await factory.CyclesAsync(2);
        Assert.Null(await factory.LatestOutageAsync());
        Assert.False(factory.Services.GetRequiredService<ConnectivityState>().OutageOpen);

        await factory.CycleAsync();

        var outage = await factory.LatestOutageAsync();
        Assert.NotNull(outage);
        Assert.Equal(OutageStatus.Active, outage.Status);
        Assert.Equal(OutageScope.Wan, outage.Scope);
        Assert.Equal(Noon, outage.StartedAt);
        Assert.True(factory.Services.GetRequiredService<ConnectivityState>().OutageOpen);
        Assert.True(outage.NotificationSent);
        var message = Assert.Single(factory.Provider.Sent);
        Assert.Equal("connection.outage", message.Event);
        Assert.Equal("Internet outage", message.Title);

        await factory.CyclesAsync(3);
        Assert.Single(factory.Provider.Sent);
        Assert.Equal(1, await factory.WithScopeAsync(async services =>
            (await services.GetRequiredService<IOutageRepository>().GetRecentOutagesAsync(10, CancellationToken.None)).Count));
    }

    [Fact]
    public async Task Flapping_below_the_threshold_never_opens_an_outage()
    {
        await using var factory = new ProbeApiFactory();
        _ = factory.CreateClient();

        for (var round = 0; round < 4; round++)
        {
            factory.Executor.Failing(IsInternet);
            await factory.CyclesAsync(2);
            factory.Executor.EverythingUp();
            await factory.CycleAsync();
        }

        Assert.Null(await factory.LatestOutageAsync());
    }

    [Fact]
    public async Task Recovery_needs_consecutive_healthy_cycles_and_ends_at_the_first_healthy_one()
    {
        await using var factory = new ProbeApiFactory();
        _ = factory.CreateClient();
        await factory.AddWebhookDestinationAsync();
        factory.Executor.Failing(IsInternet);
        await factory.CyclesAsync(3);
        var recoveryStart = factory.Time.GetUtcNow().UtcDateTime;

        factory.Executor.EverythingUp();
        await factory.CyclesAsync(2);
        Assert.Equal(OutageStatus.Active, (await factory.LatestOutageAsync())!.Status);

        factory.Executor.Failing(IsInternet);
        await factory.CycleAsync();
        factory.Executor.EverythingUp();
        recoveryStart = factory.Time.GetUtcNow().UtcDateTime;
        await factory.CyclesAsync(3);

        var outage = (await factory.LatestOutageAsync())!;
        Assert.Equal(OutageStatus.Recovered, outage.Status);
        Assert.Equal(recoveryStart, outage.EndedAt);
        Assert.False(factory.Services.GetRequiredService<ConnectivityState>().OutageOpen);
        Assert.Equal(["connection.outage", "connection.restored"], factory.Provider.Sent.Select(message => message.Event));
        Assert.Contains("Connection restored after", factory.Provider.Sent[1].Body);
        Assert.True(outage.RecoveryNotificationSent);
    }

    [Fact]
    public async Task Everything_down_including_the_gateway_is_a_local_network_outage()
    {
        await using var factory = new ProbeApiFactory();
        _ = factory.CreateClient();
        factory.Executor.EverythingDownExcept();

        await factory.CyclesAsync(3);

        Assert.Equal(OutageScope.LocalNetwork, (await factory.LatestOutageAsync())!.Scope);
    }

    [Fact]
    public async Task Failing_lookups_alone_open_a_dns_outage()
    {
        await using var factory = new ProbeApiFactory();
        _ = factory.CreateClient();
        factory.Executor.Failing(request => request.Kind == ProbeKind.Dns);

        await factory.CyclesAsync(3);

        var outage = (await factory.LatestOutageAsync())!;
        Assert.Equal(OutageScope.Dns, outage.Scope);
    }

    [Fact]
    public async Task The_scope_follows_the_connection_while_an_outage_stays_open()
    {
        await using var factory = new ProbeApiFactory();
        _ = factory.CreateClient();
        factory.Executor.Failing(IsInternet);
        await factory.CyclesAsync(3);
        Assert.Equal(OutageScope.Wan, (await factory.LatestOutageAsync())!.Scope);

        factory.Executor.EverythingDownExcept();
        await factory.CycleAsync();

        Assert.Equal(OutageScope.LocalNetwork, (await factory.LatestOutageAsync())!.Scope);
    }

    [Fact]
    public async Task Failures_during_a_speed_test_are_recorded_but_do_not_count_towards_an_outage()
    {
        await using var factory = new ProbeApiFactory();
        _ = factory.CreateClient();
        var coordinator = factory.Services.GetRequiredService<SpeedTestCoordinator>();
        var running = coordinator.TryStart(SpeedTestTrigger.Manual);
        Assert.NotNull(running);
        factory.Executor.Failing(IsInternet);

        await factory.CyclesAsync(5);

        Assert.Null(await factory.LatestOutageAsync());
        var flagged = await factory.WithScopeAsync(async services =>
            (await services.GetRequiredService<IProbeRepository>()
                .GetSamplesSinceAsync(Noon, CancellationToken.None)).Count(sample => sample.DuringSpeedTest));
        Assert.Equal(25, flagged);

        factory.ReleaseSpeedTest.SetResult();
        await running.WaitAsync(TimeSpan.FromSeconds(5));
        await factory.CyclesAsync(3);

        Assert.NotNull(await factory.LatestOutageAsync());
    }

    [Fact]
    public async Task An_unsent_outage_notice_is_delivered_by_the_retry_pass_once_the_provider_works()
    {
        await using var factory = new ProbeApiFactory();
        _ = factory.CreateClient();
        await factory.AddWebhookDestinationAsync();
        factory.Provider.Failing = true;
        factory.Executor.Failing(IsInternet);

        await factory.CyclesAsync(3);

        var outage = (await factory.LatestOutageAsync())!;
        Assert.False(outage.NotificationSent);
        Assert.Empty(factory.Provider.Sent);

        factory.Provider.Failing = false;
        await factory.WithScopeAsync(async services =>
        {
            await services.GetRequiredService<NotificationDispatcher>().DispatchPendingAsync(CancellationToken.None);
            return 0;
        });

        Assert.True((await factory.LatestOutageAsync())!.NotificationSent);
        Assert.Single(factory.Provider.Sent);
    }

    [Fact]
    public async Task An_outage_left_open_by_a_restart_is_closed_as_interrupted_at_the_last_observation()
    {
        await using var factory = new ProbeApiFactory();
        _ = factory.CreateClient();
        factory.Executor.Failing(IsInternet);
        await factory.CyclesAsync(4);
        var lastObserved = (await factory.LatestOutageAsync())!.LastObservedAt;
        factory.Time.Advance(TimeSpan.FromHours(2));

        var transition = await factory.WithScopeAsync(services =>
            services.GetRequiredService<OutageService>().CloseInterruptedAsync(CancellationToken.None));

        var outage = (await factory.LatestOutageAsync())!;
        Assert.Equal(OutageStatus.Recovered, outage.Status);
        Assert.Equal(lastObserved, outage.EndedAt);
        Assert.Equal(OutageClosureReasons.Interrupted, outage.ClosureReason);
        Assert.Equal(NotificationTrigger.Recovered, transition!.Trigger);
        Assert.False(factory.Services.GetRequiredService<ConnectivityState>().OutageOpen);
        Assert.Contains("Monitoring was interrupted", NotificationMessageBuilder.Build(NotificationTrigger.Recovered, outage).Body);
    }

    [Fact]
    public async Task Turning_monitoring_off_closes_an_open_outage()
    {
        await using var factory = new ProbeApiFactory();
        _ = factory.CreateClient();
        factory.Executor.Failing(IsInternet);
        await factory.CyclesAsync(3);

        var transition = await factory.WithScopeAsync(services => services.GetRequiredService<OutageService>()
            .CloseForDisabledMonitoringAsync(factory.Time.GetUtcNow().UtcDateTime, CancellationToken.None));

        Assert.Equal(OutageClosureReasons.MonitoringDisabled, (await factory.LatestOutageAsync())!.ClosureReason);
        Assert.NotNull(transition);
        Assert.Null(await factory.WithScopeAsync(services => services.GetRequiredService<OutageService>()
            .CloseForDisabledMonitoringAsync(factory.Time.GetUtcNow().UtcDateTime, CancellationToken.None)));
    }

    [Fact]
    public async Task Disabled_targets_are_not_probed()
    {
        await using var factory = new ProbeApiFactory();
        _ = factory.CreateClient();
        await factory.WithScopeAsync(async services =>
        {
            var repository = services.GetRequiredService<IProbeRepository>();
            foreach (var target in await repository.ListTargetsAsync(false, CancellationToken.None))
            {
                target.Enabled = target.Role == ProbeRole.Internet && target.Target == "1.1.1.1";
            }

            await repository.SaveChangesAsync(CancellationToken.None);
            return 0;
        });

        var result = await factory.CycleAsync();

        Assert.Equal("1.1.1.1", Assert.Single(result.Results).Target.Target);
    }
}
