using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Wanetra.Domain;
using Wanetra.Infrastructure.Persistence;

namespace Wanetra.Api.Tests;

public class ReliabilityMigrationTests
{
    [Fact]
    public async Task Upgrade_backfills_failure_kind_and_individual_delivery_state()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<WanetraDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var db = new WanetraDbContext(options);
        await db.Database.MigrateAsync("20260921101252_AddAlertState");

        await db.Database.ExecuteSqlRawAsync("""
            INSERT INTO "SpeedTestResults" ("Timestamp", "Engine", "Success", "ErrorMessage")
            VALUES ('2026-09-24T10:00:00Z', 'librespeed', 0, 'legacy failure');

            INSERT INTO "DegradationEvents"
                ("StartedAt", "Status", "Reason", "NotificationSent", "RecoveryNotificationSent",
                 "ConsecutiveHealthyMeasurements", "ConsecutiveUnhealthyMeasurements")
            VALUES
                ('2026-09-24T10:00:00Z', 'Active', 'pending incident', 0, 0, 0, 1),
                ('2026-09-23T10:00:00Z', 'Recovered', 'delivered incident', 1, 1, 0, 0);

            INSERT INTO "NotificationConfigurations"
                ("Provider", "Enabled", "ConfigurationJson", "CreatedAt", "UpdatedAt")
            VALUES
                ('ntfy', 1, '{{"serverUrl":"https://ntfy.sh","topic":"wanetra"}}', '2026-09-24T00:00:00Z', '2026-09-24T00:00:00Z'),
                ('webhook', 1, '{{"url":"https://example.com/hook"}}', '2026-09-24T00:00:00Z', '2026-09-24T00:00:00Z');
            """);

        await db.Database.MigrateAsync();

        var failedResult = await db.SpeedTestResults.SingleAsync();
        Assert.Equal(SpeedTestFailureKind.LocalExecutionFailure, failedResult.FailureKind);

        var events = await db.DegradationEvents
            .Include(degradationEvent => degradationEvent.NotificationDeliveries)
            .OrderBy(degradationEvent => degradationEvent.StartedAt)
            .ToListAsync();
        var deliveredEvent = events[0];
        Assert.True(deliveredEvent.OpenedDeliveryInitialized);
        Assert.True(deliveredEvent.RecoveryDeliveryInitialized);
        Assert.All(deliveredEvent.NotificationDeliveries, delivery =>
            Assert.Equal(NotificationDeliveryStatus.Delivered, delivery.Status));
        Assert.Equal(4, deliveredEvent.NotificationDeliveries.Count);

        var pendingEvent = events[1];
        Assert.True(pendingEvent.OpenedDeliveryInitialized);
        Assert.Equal(2, pendingEvent.NotificationDeliveries.Count);
        Assert.All(pendingEvent.NotificationDeliveries, delivery =>
            Assert.Equal(NotificationDeliveryStatus.Pending, delivery.Status));
    }
}
