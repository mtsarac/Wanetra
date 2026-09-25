using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wanetra.Domain;
using Wanetra.Infrastructure.Persistence;

namespace Wanetra.Api.Tests.SpeedTests;

public class AlertStatePersistenceTests
{
    [Fact]
    public async Task Alert_state_counters_survive_new_scope()
    {
        await using var factory = new WanetraApiFactory();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanetraDbContext>();
            var state = await db.AlertStates.SingleAsync();
            state.ConsecutiveUnhealthyMeasurements = 2;
            state.ConsecutiveHealthyMeasurements = 1;
            state.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }

        using var reloadedScope = factory.Services.CreateScope();
        var reloaded = await reloadedScope.ServiceProvider.GetRequiredService<WanetraDbContext>()
            .AlertStates.SingleAsync();

        Assert.Equal(2, reloaded.ConsecutiveUnhealthyMeasurements);
        Assert.Equal(1, reloaded.ConsecutiveHealthyMeasurements);
    }
}
