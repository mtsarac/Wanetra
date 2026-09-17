using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wanetra.Domain;
using Wanetra.Infrastructure;
using Wanetra.Infrastructure.Persistence;

namespace Wanetra.Api.Tests;

public class DatabaseInitializationTests(WanetraApiFactory factory) : IClassFixture<WanetraApiFactory>
{
    [Fact]
    public async Task Startup_creates_database_in_empty_data_directory()
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<WanetraDbContext>();

        Assert.True(File.Exists(Path.Combine(factory.DataPath, DependencyInjection.DatabaseFileName)));
        Assert.Empty(await dbContext.Database.GetPendingMigrationsAsync());
        Assert.False(dbContext.Database.HasPendingModelChanges());
    }

    [Fact]
    public async Task Timestamps_are_read_back_as_utc()
    {
        var timestamp = new DateTime(2026, 9, 17, 15, 0, 0, DateTimeKind.Utc);

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<WanetraDbContext>();
            dbContext.SpeedTestResults.Add(new SpeedTestResult { Engine = "librespeed", Timestamp = timestamp, Success = true });
            await dbContext.SaveChangesAsync();
        }

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<WanetraDbContext>();
            var stored = await dbContext.SpeedTestResults.SingleAsync(x => x.Timestamp == timestamp);

            Assert.Equal(DateTimeKind.Utc, stored.Timestamp.Kind);
            Assert.Equal(timestamp, stored.Timestamp);
        }
    }
}
