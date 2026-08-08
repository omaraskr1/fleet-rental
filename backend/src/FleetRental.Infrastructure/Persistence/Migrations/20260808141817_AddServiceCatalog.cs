using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FleetRental.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddServiceCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ServiceTypeId",
                table: "ServiceRecords",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ServiceTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    IntervalKm = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceTypes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceRecords_CarId_ServiceTypeId_PerformedAt",
                table: "ServiceRecords",
                columns: new[] { "CarId", "ServiceTypeId", "PerformedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceRecords_ServiceTypeId",
                table: "ServiceRecords",
                column: "ServiceTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceTypes_TenantId_IsActive",
                table: "ServiceTypes",
                columns: new[] { "TenantId", "IsActive" });

            migrationBuilder.AddForeignKey(
                name: "FK_ServiceRecords_ServiceTypes_ServiceTypeId",
                table: "ServiceRecords",
                column: "ServiceTypeId",
                principalTable: "ServiceTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ServiceRecords_ServiceTypes_ServiceTypeId",
                table: "ServiceRecords");

            migrationBuilder.DropTable(
                name: "ServiceTypes");

            migrationBuilder.DropIndex(
                name: "IX_ServiceRecords_CarId_ServiceTypeId_PerformedAt",
                table: "ServiceRecords");

            migrationBuilder.DropIndex(
                name: "IX_ServiceRecords_ServiceTypeId",
                table: "ServiceRecords");

            migrationBuilder.DropColumn(
                name: "ServiceTypeId",
                table: "ServiceRecords");
        }
    }
}
