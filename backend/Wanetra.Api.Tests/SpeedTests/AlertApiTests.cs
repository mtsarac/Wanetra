using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wanetra.Domain;
using Wanetra.Infrastructure.Persistence;

namespace Wanetra.Api.Tests.SpeedTests;

public class AlertApiTests
{
    [Fact]
    public async Task Active_alert_returns_404_when_no_event_is_open()
    {
        await using var factory = new WanetraApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/alerts/active");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Active_alert_returns_open_event()
    {
        await using var factory = new WanetraApiFactory();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanetraDbContext>();
            db.DegradationEvents.Add(new DegradationEvent
            {
                StartedAt = DateTime.UtcNow,
                Status = DegradationStatus.Active,
                Reason = "download below threshold",
            });
            await db.SaveChangesAsync();
        }
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/alerts/active");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("download below threshold", body);
        Assert.Contains("active", body, StringComparison.OrdinalIgnoreCase);
    }
}
