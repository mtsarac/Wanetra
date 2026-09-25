using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wanetra.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReliabilityCorrectness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {

            migrationBuilder.AddColumn<string>(
                name: "FailureKind",
                table: "SpeedTestResults",
                type: "TEXT",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClosureReason",
                table: "DegradationEvents",
                type: "TEXT",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "OpenedDeliveryInitialized",
                table: "DegradationEvents",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "RecoveryDeliveryInitialized",
                table: "DegradationEvents",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "NotificationDeliveries",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DegradationEventId = table.Column<long>(type: "INTEGER", nullable: false),
                    Trigger = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    ConfigurationId = table.Column<int>(type: "INTEGER", nullable: false),
                    Provider = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    AttemptCount = table.Column<int>(type: "INTEGER", nullable: false),
                    LastAttemptAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DeliveredAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastErrorSummary = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationDeliveries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NotificationDeliveries_DegradationEvents_DegradationEventId",
                        column: x => x.DegradationEventId,
                        principalTable: "DegradationEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationDeliveries_DegradationEventId_Trigger_ConfigurationId",
                table: "NotificationDeliveries",
                columns: new[] { "DegradationEventId", "Trigger", "ConfigurationId" },
                unique: true);
            migrationBuilder.Sql("""
                UPDATE "SpeedTestResults"
                SET "FailureKind" = 'LocalExecutionFailure'
                WHERE "Success" = 0;

                UPDATE "DegradationEvents"
                SET "OpenedDeliveryInitialized" = 1,
                    "RecoveryDeliveryInitialized" = CASE WHEN "Status" = 'Recovered' THEN 1 ELSE 0 END;

                INSERT INTO "NotificationDeliveries"
                    ("DegradationEventId", "Trigger", "ConfigurationId", "Provider", "Status",
                     "AttemptCount", "LastAttemptAt", "DeliveredAt")
                SELECT event."Id", 'Opened', configuration."Id", configuration."Provider",
                       CASE WHEN event."NotificationSent" = 1 THEN 'Delivered' ELSE 'Pending' END,
                       CASE WHEN event."NotificationSent" = 1 THEN 1 ELSE 0 END,
                       CASE WHEN event."NotificationSent" = 1 THEN event."StartedAt" ELSE NULL END,
                       CASE WHEN event."NotificationSent" = 1 THEN event."StartedAt" ELSE NULL END
                FROM "DegradationEvents" AS event
                CROSS JOIN "NotificationConfigurations" AS configuration
                WHERE configuration."Enabled" = 1;

                INSERT INTO "NotificationDeliveries"
                    ("DegradationEventId", "Trigger", "ConfigurationId", "Provider", "Status",
                     "AttemptCount", "LastAttemptAt", "DeliveredAt")
                SELECT opened."DegradationEventId", 'Recovered', opened."ConfigurationId", opened."Provider",
                       CASE WHEN event."RecoveryNotificationSent" = 1 THEN 'Delivered' ELSE 'Pending' END,
                       CASE WHEN event."RecoveryNotificationSent" = 1 THEN 1 ELSE 0 END,
                       CASE WHEN event."RecoveryNotificationSent" = 1 THEN event."EndedAt" ELSE NULL END,
                       CASE WHEN event."RecoveryNotificationSent" = 1 THEN event."EndedAt" ELSE NULL END
                FROM "NotificationDeliveries" AS opened
                INNER JOIN "DegradationEvents" AS event ON event."Id" = opened."DegradationEventId"
                WHERE opened."Trigger" = 'Opened'
                  AND event."Status" = 'Recovered';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NotificationDeliveries");

            migrationBuilder.DropColumn(
                name: "FailureKind",
                table: "SpeedTestResults");

            migrationBuilder.DropColumn(
                name: "ClosureReason",
                table: "DegradationEvents");

            migrationBuilder.DropColumn(
                name: "OpenedDeliveryInitialized",
                table: "DegradationEvents");

            migrationBuilder.DropColumn(
                name: "RecoveryDeliveryInitialized",
                table: "DegradationEvents");

        }
    }
}
