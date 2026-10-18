using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Wanetra.Domain;
using Wanetra.Infrastructure.Persistence;

namespace Wanetra.Api.Tests.Probes;

public class ProbeMigrationTests
{
    [Fact]
    public async Task Upgrading_keeps_existing_deliveries_attached_to_their_degradation_events()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<WanetraDbContext>().UseSqlite(connection).Options;
        await using var db = new WanetraDbContext(options);
        await db.Database.MigrateAsync("20261018233014_AddAuth");

        await db.Database.ExecuteSqlRawAsync("""
            INSERT INTO "DegradationEvents"
                ("StartedAt", "Status", "Reason", "NotificationSent", "RecoveryNotificationSent",
                 "OpenedDeliveryInitialized", "RecoveryDeliveryInitialized",
                 "ConsecutiveHealthyMeasurements", "ConsecutiveUnhealthyMeasurements")
            VALUES ('2026-10-01T10:00:00Z', 'Recovered', 'old incident', 1, 1, 1, 1, 0, 0);

            INSERT INTO "NotificationDeliveries"
                ("DegradationEventId", "Trigger", "ConfigurationId", "Provider", "Status", "AttemptCount")
            VALUES (1, 'Opened', 7, 'webhook', 'Delivered', 1), (1, 'Recovered', 7, 'webhook', 'Delivered', 1);
            """);

        await db.Database.MigrateAsync();

        var degradation = await db.DegradationEvents.Include(e => e.NotificationDeliveries).SingleAsync();
        Assert.Equal(2, degradation.NotificationDeliveries.Count);
        Assert.All(degradation.NotificationDeliveries, delivery =>
        {
            Assert.Equal(degradation.Id, delivery.DegradationEventId);
            Assert.Null(delivery.OutageId);
            Assert.Equal(NotificationDeliveryStatus.Delivered, delivery.Status);
        });

        db.Outages.Add(new Outage
        {
            StartedAt = DateTime.UtcNow,
            Reason = "x",
            LastObservedAt = DateTime.UtcNow,
            NotificationDeliveries =
            [
                new NotificationDelivery { Trigger = NotificationTrigger.Opened, ConfigurationId = 7, Provider = "webhook" },
            ],
        });
        await db.SaveChangesAsync();
        Assert.Equal(1, await db.NotificationDeliveries.CountAsync(delivery => delivery.OutageId != null));
    }
}
