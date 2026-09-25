using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wanetra.Api.Contracts;
using Wanetra.Domain;
using Wanetra.Infrastructure.Persistence;

namespace Wanetra.Api.Tests.SpeedTests;

public class DegradationEventApiTests
{
    [Fact]
    public async Task Event_history_returns_newest_events_with_persisted_details()
    {
        await using var factory = new WanetraApiFactory();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanetraDbContext>();
            var now = DateTime.UtcNow;
            db.DegradationEvents.AddRange(
                new DegradationEvent { StartedAt = now.AddHours(-2), EndedAt = now.AddHours(-1), Status = DegradationStatus.Recovered, Reason = "older incident" },
                new DegradationEvent { StartedAt = now, Status = DegradationStatus.Active, Reason = "recent incident", WorstDownloadMbps = 42 });
            await db.SaveChangesAsync();
        }

        using var client = factory.CreateClient();
        var events = await client.GetFromJsonAsync<DegradationEventResponse[]>("/api/alerts/events?count=1");

        var recent = Assert.Single(events!);
        Assert.Equal("recent incident", recent.Reason);
        Assert.Equal("active", recent.Status);
        Assert.Equal(42, recent.WorstDownloadMbps);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public async Task Event_history_rejects_out_of_range_count(int count)
    {
        await using var factory = new WanetraApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/alerts/events?count={count}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
