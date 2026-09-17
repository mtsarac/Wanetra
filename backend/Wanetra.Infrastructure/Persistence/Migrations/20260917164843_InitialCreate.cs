using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wanetra.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AlertRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    MinDownloadMbps = table.Column<double>(type: "REAL", nullable: true),
                    MinUploadMbps = table.Column<double>(type: "REAL", nullable: true),
                    MaxLatencyMs = table.Column<double>(type: "REAL", nullable: true),
                    MaxJitterMs = table.Column<double>(type: "REAL", nullable: true),
                    MaxPacketLossPercent = table.Column<double>(type: "REAL", nullable: true),
                    DownloadBaselineDropPercent = table.Column<double>(type: "REAL", nullable: true),
                    UploadBaselineDropPercent = table.Column<double>(type: "REAL", nullable: true),
                    ConsecutiveFailuresRequired = table.Column<int>(type: "INTEGER", nullable: false),
                    ConsecutiveRecoveriesRequired = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AlertRules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DegradationEvents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EndedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Reason = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: false),
                    BaselineDownloadMbps = table.Column<double>(type: "REAL", nullable: true),
                    WorstDownloadMbps = table.Column<double>(type: "REAL", nullable: true),
                    BaselineUploadMbps = table.Column<double>(type: "REAL", nullable: true),
                    WorstUploadMbps = table.Column<double>(type: "REAL", nullable: true),
                    MaxLatencyMs = table.Column<double>(type: "REAL", nullable: true),
                    MaxJitterMs = table.Column<double>(type: "REAL", nullable: true),
                    MaxPacketLossPercent = table.Column<double>(type: "REAL", nullable: true),
                    NotificationSent = table.Column<bool>(type: "INTEGER", nullable: false),
                    RecoveryNotificationSent = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DegradationEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NotificationConfigurations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Provider = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    ConfigurationJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationConfigurations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ScheduleSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    CronExpression = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Timezone = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduleSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SpeedTestResults",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Engine = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    DownloadMbps = table.Column<double>(type: "REAL", nullable: true),
                    UploadMbps = table.Column<double>(type: "REAL", nullable: true),
                    LatencyMs = table.Column<double>(type: "REAL", nullable: true),
                    JitterMs = table.Column<double>(type: "REAL", nullable: true),
                    PacketLossPercent = table.Column<double>(type: "REAL", nullable: true),
                    ServerName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    ServerLocation = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    ServerId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Isp = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    ExternalIp = table.Column<string>(type: "TEXT", maxLength: 45, nullable: true),
                    DurationMs = table.Column<long>(type: "INTEGER", nullable: true),
                    Success = table.Column<bool>(type: "INTEGER", nullable: false),
                    ErrorMessage = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SpeedTestResults", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DegradationEvents_StartedAt",
                table: "DegradationEvents",
                column: "StartedAt");

            migrationBuilder.CreateIndex(
                name: "IX_DegradationEvents_Status",
                table: "DegradationEvents",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationConfigurations_Provider",
                table: "NotificationConfigurations",
                column: "Provider");

            migrationBuilder.CreateIndex(
                name: "IX_SpeedTestResults_Timestamp",
                table: "SpeedTestResults",
                column: "Timestamp");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AlertRules");

            migrationBuilder.DropTable(
                name: "DegradationEvents");

            migrationBuilder.DropTable(
                name: "NotificationConfigurations");

            migrationBuilder.DropTable(
                name: "ScheduleSettings");

            migrationBuilder.DropTable(
                name: "SpeedTestResults");
        }
    }
}
