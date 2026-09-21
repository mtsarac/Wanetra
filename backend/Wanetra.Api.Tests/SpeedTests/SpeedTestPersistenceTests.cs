using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Wanetra.Application.SpeedTests;
using Wanetra.Domain;
using Wanetra.Infrastructure.Persistence;

namespace Wanetra.Api.Tests.SpeedTests;

public class SpeedTestPersistenceTests
{
    [Fact]
    public async Task Successful_persisted_result_is_evaluated_by_alert_engine()
    {
        await using var factory = new StubEngineApiFactory();
        using (var setupScope = factory.Services.CreateScope())
        {
            var db = setupScope.ServiceProvider.GetRequiredService<WanetraDbContext>();
            db.AlertRules.Add(new AlertRule
            {
                Name = "default",
                Enabled = true,
                MinDownloadMbps = 100,
                ConsecutiveFailuresRequired = 1,
                ConsecutiveRecoveriesRequired = 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
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
}
