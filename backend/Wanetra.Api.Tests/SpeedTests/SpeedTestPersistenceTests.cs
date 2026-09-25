using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Wanetra.Application.Notifications;
using Wanetra.Application.SpeedTests;
using Wanetra.Domain;
using Wanetra.Infrastructure.Persistence;

namespace Wanetra.Api.Tests.SpeedTests;

public class SpeedTestPersistenceTests
{
    [Fact]
    public async Task Failed_measurement_is_persisted_evaluated_and_notified()
    {
        var provider = new RecordingNotificationProvider();
        await using var factory = new FailedEngineApiFactory(provider);
        using (var setupScope = factory.Services.CreateScope())
        {
            var db = setupScope.ServiceProvider.GetRequiredService<WanetraDbContext>();
            var rule = await db.AlertRules.SingleAsync();
            rule.ConsecutiveFailuresRequired = 1;
            await db.NotificationConfigurations.AddAsync(new NotificationConfiguration
            {
                Provider = "webhook",
                Enabled = true,
                ConfigurationJson = """{"url":"https://example.com/hook"}""",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        await factory.Services.GetRequiredService<SpeedTestCoordinator>().TryStart(SpeedTestTrigger.Manual)!;

        using var verifyScope = factory.Services.CreateScope();
        var dbContext = verifyScope.ServiceProvider.GetRequiredService<WanetraDbContext>();
        var result = await dbContext.SpeedTestResults.SingleAsync();
        var degradationEvent = await dbContext.DegradationEvents.SingleAsync();
        Assert.False(result.Success);
        Assert.Equal(SpeedTestFailureKind.NetworkFailure, result.FailureKind);
        Assert.Equal("The DNS lookup failed.", result.ErrorMessage);
        Assert.Equal("network test failed", degradationEvent.Reason);
        Assert.True(degradationEvent.NotificationSent);
    }
    [Fact]
    public async Task Local_execution_failure_is_persisted_and_counted_without_alerting()
    {
        var metrics = new RecordingMetrics();
        await using var factory = new FailedEngineApiFactory(
            new RecordingNotificationProvider(),
            SpeedTestFailureKind.LocalExecutionFailure,
            metrics);
        using (var setupScope = factory.Services.CreateScope())
        {
            var rule = await setupScope.ServiceProvider.GetRequiredService<WanetraDbContext>().AlertRules.SingleAsync();
            rule.ConsecutiveFailuresRequired = 1;
            await setupScope.ServiceProvider.GetRequiredService<WanetraDbContext>().SaveChangesAsync();
        }

        await factory.Services.GetRequiredService<SpeedTestCoordinator>().TryStart(SpeedTestTrigger.Manual)!;

        using var verifyScope = factory.Services.CreateScope();
        var db = verifyScope.ServiceProvider.GetRequiredService<WanetraDbContext>();
        var result = await db.SpeedTestResults.SingleAsync();
        Assert.False(result.Success);
        Assert.Equal(SpeedTestFailureKind.LocalExecutionFailure, result.FailureKind);
        Assert.Equal(1, metrics.FailedResults);
        Assert.Empty(await db.DegradationEvents.ToListAsync());
    }

    [Fact]
    public async Task Cancelled_measurement_does_not_create_degradation()
    {
        await using var factory = new WaitingEngineApiFactory();
        using (var setupScope = factory.Services.CreateScope())
        {
            var rule = await setupScope.ServiceProvider.GetRequiredService<WanetraDbContext>().AlertRules.SingleAsync();
            rule.ConsecutiveFailuresRequired = 1;
            await setupScope.ServiceProvider.GetRequiredService<WanetraDbContext>().SaveChangesAsync();
        }

        using var cancellation = new CancellationTokenSource();
        var running = factory.Services.GetRequiredService<SpeedTestCoordinator>()
            .TryStart(SpeedTestTrigger.Manual, cancellation.Token);
        await cancellation.CancelAsync();
        await running!;

        using var verifyScope = factory.Services.CreateScope();
        var db = verifyScope.ServiceProvider.GetRequiredService<WanetraDbContext>();
        Assert.Empty(await db.SpeedTestResults.ToListAsync());
        Assert.Empty(await db.DegradationEvents.ToListAsync());
    }

    [Fact]
    public async Task Successful_persisted_result_is_evaluated_by_alert_engine()
    {
        await using var factory = new StubEngineApiFactory();
        using (var setupScope = factory.Services.CreateScope())
        {
            var db = setupScope.ServiceProvider.GetRequiredService<WanetraDbContext>();
            var rule = await db.AlertRules.SingleAsync();
            rule.Name = "default";
            rule.Enabled = true;
            rule.MinDownloadMbps = 100;
            rule.ConsecutiveFailuresRequired = 1;
            rule.ConsecutiveRecoveriesRequired = 1;
            await db.SaveChangesAsync();
        }

        var coordinator = factory.Services.GetRequiredService<SpeedTestCoordinator>();
        await coordinator.TryStart(SpeedTestTrigger.Manual)!;

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<WanetraDbContext>();
        Assert.Single(await dbContext.DegradationEvents.ToListAsync());
    }

    [Fact]
    public async Task Result_is_written_to_the_database()
    {
        await using var factory = new StubEngineApiFactory();
        var coordinator = factory.Services.GetRequiredService<SpeedTestCoordinator>();

        await coordinator.TryStart(SpeedTestTrigger.Manual)!;

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<WanetraDbContext>();
        var stored = await dbContext.SpeedTestResults.SingleAsync();

        Assert.Equal("stub", stored.Engine);
        Assert.True(stored.Success);
        Assert.Equal(50, stored.DownloadMbps);
        Assert.Equal(DateTimeKind.Utc, stored.Timestamp.Kind);
    }

    private sealed class StubEngineApiFactory : WanetraApiFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ISpeedTestEngine>();
                services.AddScoped<ISpeedTestEngine>(_ => new StubSpeedTestEngine(_ =>
                    Task.FromResult(new SpeedTestResult { Engine = "stub", DownloadMbps = 50 })));
            });
        }
    }
    private sealed class FailedEngineApiFactory(
        RecordingNotificationProvider provider,
        SpeedTestFailureKind failureKind = SpeedTestFailureKind.NetworkFailure,
        RecordingMetrics? metrics = null) : WanetraApiFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ISpeedTestEngine>();
                services.AddScoped<ISpeedTestEngine>(_ => new StubSpeedTestEngine(_ =>
                    throw new SpeedTestExecutionException(failureKind, "The DNS lookup failed.")));
                services.RemoveAll<INotificationProvider>();
                services.AddSingleton<INotificationProvider>(provider);
                if (metrics is not null)
                {
                    services.RemoveAll<IPrometheusMetrics>();
                    services.AddSingleton<IPrometheusMetrics>(metrics);
                }
            });
        }
    }

    private sealed class WaitingEngineApiFactory : WanetraApiFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ISpeedTestEngine>();
                services.AddScoped<ISpeedTestEngine>(_ => new StubSpeedTestEngine(async token =>
                {
                    await Task.Delay(Timeout.Infinite, token);
                    return new SpeedTestResult { Engine = "stub" };
                }));
            });
        }
    }

    private sealed class RecordingNotificationProvider : INotificationProvider
    {
        public string Name => "webhook";
        public List<NotificationMessage> Sent { get; } = [];

        public Task SendAsync(NotificationMessage message, string configurationJson, CancellationToken cancellationToken)
        {
            Sent.Add(message);
            return Task.CompletedTask;
        }
    }
    private sealed class RecordingMetrics : IPrometheusMetrics
    {
        public int FailedResults { get; private set; }

        public void Initialize(SpeedTestResult? latestResult, SpeedTestResult? latestSuccessfulResult, bool connectionDegraded)
        {
        }

        public void RecordSpeedTest(SpeedTestResult result)
        {
            if (!result.Success)
            {
                FailedResults++;
            }
        }

        public void SetConnectionDegraded(bool degraded)
        {
        }
    }
}
