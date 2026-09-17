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
        Assert.Equal(812.3, stored.DownloadMbps);
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
                    Task.FromResult(new SpeedTestResult { Engine = "stub", DownloadMbps = 812.3 })));
            });
        }
    }
}
