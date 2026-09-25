using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wanetra.Domain;
using Wanetra.Infrastructure.Persistence;

namespace Wanetra.Api.Tests;

public class DataRetentionTests(WanetraApiFactory factory) : IClassFixture<WanetraApiFactory>
{
    [Fact]
    public async Task Cleanup_deletes_only_results_strictly_older_than_cutoff()
    {
        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<WanetraDbContext>();
        var repository = scope.ServiceProvider.GetRequiredService<ISpeedTestResultRepository>();
        var cutoff = DateTime.UtcNow.AddDays(-30);
        database.SpeedTestResults.AddRange(
            new SpeedTestResult { Engine = "test", Timestamp = cutoff.AddTicks(-1), Success = true },
            new SpeedTestResult { Engine = "test", Timestamp = cutoff, Success = true },
            new SpeedTestResult { Engine = "test", Timestamp = cutoff.AddTicks(1), Success = false });
        await database.SaveChangesAsync();

        var deleted = await repository.DeleteOlderThanAsync(cutoff, CancellationToken.None);
        var remaining = await database.SpeedTestResults.OrderBy(result => result.Timestamp).ToListAsync();

        Assert.Equal(1, deleted);
        Assert.Equal(2, remaining.Count);
        Assert.Equal(cutoff, remaining[0].Timestamp);
        Assert.False(remaining[1].Success);
    }
}
