using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wanetra.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProbesAndOutages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<long>(
                name: "DegradationEventId",
                table: "NotificationDeliveries",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "INTEGER");

            migrationBuilder.AddColumn<long>(
                name: "OutageId",
                table: "NotificationDeliveries",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Outages",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EndedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Scope = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Reason = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    ClosureReason = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    LastObservedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    NotificationSent = table.Column<bool>(type: "INTEGER", nullable: false),
                    RecoveryNotificationSent = table.Column<bool>(type: "INTEGER", nullable: false),
                    OpenedDeliveryInitialized = table.Column<bool>(type: "INTEGER", nullable: false),
                    RecoveryDeliveryInitialized = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Outages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProbeTargets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false, collation: "NOCASE"),
                    Kind = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Role = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Target = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
                    Port = table.Column<int>(type: "INTEGER", nullable: true),
                    Resolver = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProbeTargets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProbeRollups",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TargetId = table.Column<int>(type: "INTEGER", nullable: false),
                    Resolution = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    BucketStart = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Sent = table.Column<int>(type: "INTEGER", nullable: false),
                    Received = table.Column<int>(type: "INTEGER", nullable: false),
                    RttMinMs = table.Column<double>(type: "REAL", nullable: true),
                    RttAvgMs = table.Column<double>(type: "REAL", nullable: true),
                    RttMaxMs = table.Column<double>(type: "REAL", nullable: true),
                    JitterMs = table.Column<double>(type: "REAL", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProbeRollups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProbeRollups_ProbeTargets_TargetId",
                        column: x => x.TargetId,
                        principalTable: "ProbeTargets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProbeSamples",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TargetId = table.Column<int>(type: "INTEGER", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Success = table.Column<bool>(type: "INTEGER", nullable: false),
                    RttMs = table.Column<double>(type: "REAL", nullable: true),
                    Error = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    DuringSpeedTest = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProbeSamples", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProbeSamples_ProbeTargets_TargetId",
                        column: x => x.TargetId,
                        principalTable: "ProbeTargets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationDeliveries_OutageId_Trigger_ConfigurationId",
                table: "NotificationDeliveries",
                columns: new[] { "OutageId", "Trigger", "ConfigurationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Outages_StartedAt",
                table: "Outages",
                column: "StartedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Outages_Status",
                table: "Outages",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ProbeRollups_Resolution_BucketStart",
                table: "ProbeRollups",
                columns: new[] { "Resolution", "BucketStart" });

            migrationBuilder.CreateIndex(
                name: "IX_ProbeRollups_TargetId_Resolution_BucketStart",
                table: "ProbeRollups",
                columns: new[] { "TargetId", "Resolution", "BucketStart" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProbeSamples_TargetId_Timestamp",
                table: "ProbeSamples",
                columns: new[] { "TargetId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_ProbeSamples_Timestamp",
                table: "ProbeSamples",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_ProbeTargets_Name",
                table: "ProbeTargets",
                column: "Name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_NotificationDeliveries_Outages_OutageId",
                table: "NotificationDeliveries",
                column: "OutageId",
                principalTable: "Outages",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_NotificationDeliveries_Outages_OutageId",
                table: "NotificationDeliveries");

            migrationBuilder.DropTable(
                name: "Outages");

            migrationBuilder.DropTable(
                name: "ProbeRollups");

            migrationBuilder.DropTable(
                name: "ProbeSamples");

            migrationBuilder.DropTable(
                name: "ProbeTargets");

            migrationBuilder.DropIndex(
                name: "IX_NotificationDeliveries_OutageId_Trigger_ConfigurationId",
                table: "NotificationDeliveries");

            migrationBuilder.DropColumn(
                name: "OutageId",
                table: "NotificationDeliveries");

            migrationBuilder.AlterColumn<long>(
                name: "DegradationEventId",
                table: "NotificationDeliveries",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "INTEGER",
                oldNullable: true);
        }
    }
}
