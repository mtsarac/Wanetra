using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wanetra.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAlertState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ConsecutiveHealthyMeasurements",
                table: "DegradationEvents",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ConsecutiveUnhealthyMeasurements",
                table: "DegradationEvents",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "AlertStates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RuleId = table.Column<int>(type: "INTEGER", nullable: true),
                    RuleUpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ConsecutiveUnhealthyMeasurements = table.Column<int>(type: "INTEGER", nullable: false),
                    ConsecutiveHealthyMeasurements = table.Column<int>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AlertStates", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AlertStates");

            migrationBuilder.DropColumn(
                name: "ConsecutiveHealthyMeasurements",
                table: "DegradationEvents");

            migrationBuilder.DropColumn(
                name: "ConsecutiveUnhealthyMeasurements",
                table: "DegradationEvents");
        }
    }
}
