using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FleetRental.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGpsTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GpsDeviceKey",
                table: "Cars",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CarLocations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CarId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Latitude = table.Column<double>(type: "float(9)", precision: 9, scale: 6, nullable: false),
                    Longitude = table.Column<double>(type: "float(9)", precision: 9, scale: 6, nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CarLocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CarLocations_Cars_CarId",
                        column: x => x.CarId,
                        principalTable: "Cars",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Cars_GpsDeviceKey",
                table: "Cars",
                column: "GpsDeviceKey",
                unique: true,
                filter: "[GpsDeviceKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CarLocations_CarId_RecordedAt",
                table: "CarLocations",
                columns: new[] { "CarId", "RecordedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CarLocations");

            migrationBuilder.DropIndex(
                name: "IX_Cars_GpsDeviceKey",
                table: "Cars");

            migrationBuilder.DropColumn(
                name: "GpsDeviceKey",
                table: "Cars");
        }
    }
}
