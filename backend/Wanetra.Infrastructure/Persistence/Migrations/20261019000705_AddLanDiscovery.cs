using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wanetra.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLanDiscovery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "DeviceId",
                table: "ProbeTargets",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "DeviceAlertId",
                table: "NotificationDeliveries",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Devices",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Mac = table.Column<string>(type: "TEXT", maxLength: 17, nullable: false),
                    CurrentIp = table.Column<string>(type: "TEXT", maxLength: 45, nullable: true),
                    Hostname = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Vendor = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Description = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Known = table.Column<bool>(type: "INTEGER", nullable: false),
                    Watch = table.Column<bool>(type: "INTEGER", nullable: false),
                    Online = table.Column<bool>(type: "INTEGER", nullable: false),
                    RandomizedMac = table.Column<bool>(type: "INTEGER", nullable: false),
                    MissedScans = table.Column<int>(type: "INTEGER", nullable: false),
                    FirstSeenAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastSeenAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastStateChangeAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Devices", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ScanRuns",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FinishedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Networks = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    HostsFound = table.Column<int>(type: "INTEGER", nullable: false),
                    Success = table.Column<bool>(type: "INTEGER", nullable: false),
                    Error = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    Trigger = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScanRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DeviceAlerts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DeviceId = table.Column<long>(type: "INTEGER", nullable: false),
                    Kind = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EndedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DeviceLabel = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Mac = table.Column<string>(type: "TEXT", maxLength: 17, nullable: false),
                    Ip = table.Column<string>(type: "TEXT", maxLength: 45, nullable: true),
                    Vendor = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    NotificationSent = table.Column<bool>(type: "INTEGER", nullable: false),
                    RecoveryNotificationSent = table.Column<bool>(type: "INTEGER", nullable: false),
                    OpenedDeliveryInitialized = table.Column<bool>(type: "INTEGER", nullable: false),
                    RecoveryDeliveryInitialized = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceAlerts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeviceAlerts_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DeviceEvents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DeviceId = table.Column<long>(type: "INTEGER", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Type = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Detail = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeviceEvents_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProbeTargets_DeviceId",
                table: "ProbeTargets",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationDeliveries_DeviceAlertId_Trigger_ConfigurationId",
                table: "NotificationDeliveries",
                columns: new[] { "DeviceAlertId", "Trigger", "ConfigurationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DeviceAlerts_DeviceId_Kind_Status",
                table: "DeviceAlerts",
                columns: new[] { "DeviceId", "Kind", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_DeviceEvents_DeviceId_Timestamp",
                table: "DeviceEvents",
                columns: new[] { "DeviceId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_DeviceEvents_Timestamp",
                table: "DeviceEvents",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_Devices_CurrentIp",
                table: "Devices",
                column: "CurrentIp");

            migrationBuilder.CreateIndex(
                name: "IX_Devices_Mac",
                table: "Devices",
                column: "Mac",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ScanRuns_StartedAt",
                table: "ScanRuns",
                column: "StartedAt");

            migrationBuilder.AddForeignKey(
                name: "FK_NotificationDeliveries_DeviceAlerts_DeviceAlertId",
                table: "NotificationDeliveries",
                column: "DeviceAlertId",
                principalTable: "DeviceAlerts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_NotificationDeliveries_DeviceAlerts_DeviceAlertId",
                table: "NotificationDeliveries");

            migrationBuilder.DropTable(
                name: "DeviceAlerts");

            migrationBuilder.DropTable(
                name: "DeviceEvents");

            migrationBuilder.DropTable(
                name: "ScanRuns");

            migrationBuilder.DropTable(
                name: "Devices");

            migrationBuilder.DropIndex(
                name: "IX_ProbeTargets_DeviceId",
                table: "ProbeTargets");

            migrationBuilder.DropIndex(
                name: "IX_NotificationDeliveries_DeviceAlertId_Trigger_ConfigurationId",
                table: "NotificationDeliveries");

            migrationBuilder.DropColumn(
                name: "DeviceId",
                table: "ProbeTargets");

            migrationBuilder.DropColumn(
                name: "DeviceAlertId",
                table: "NotificationDeliveries");
        }
    }
}
