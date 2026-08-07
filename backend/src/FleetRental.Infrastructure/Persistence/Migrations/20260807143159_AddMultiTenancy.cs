using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FleetRental.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMultiTenancy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_Email",
                table: "Users");

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Users",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Events",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "DeviceTokens",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Cars",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "CarPhotos",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Bookings",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "BookedDays",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "Tenants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ContactEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tenants", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "UX_Users_Tenant_Email",
                table: "Users",
                columns: new[] { "TenantId", "Email" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Tenants_Code",
                table: "Tenants",
                column: "Code",
                unique: true);

            // Backfill. Without this, every pre-existing row keeps the default
            // all-zero TenantId, which matches no tenant — so the global query
            // filters would hide the entire existing dataset. The rows would still
            // be there, and every screen would simply come up empty, which is a
            // deeply confusing way to lose data.
            //
            // Everything that existed before tenancy belonged to the first and only
            // business, so it is adopted by a tenant created here. Idempotent, so
            // re-running against an already-migrated database is harmless.
            migrationBuilder.Sql(
                """
                DECLARE @TenantId uniqueidentifier;

                SELECT @TenantId = Id FROM Tenants WHERE Code = 'demo-fleet';

                IF @TenantId IS NULL
                BEGIN
                    SET @TenantId = NEWID();
                    INSERT INTO Tenants (Id, Name, Code, ContactEmail, Status, CreatedAt)
                    VALUES (@TenantId, 'Demo Fleet', 'demo-fleet', NULL, 'Active', SYSDATETIMEOFFSET());
                END

                DECLARE @Empty uniqueidentifier = '00000000-0000-0000-0000-000000000000';

                UPDATE Users       SET TenantId = @TenantId WHERE TenantId = @Empty;
                UPDATE Cars        SET TenantId = @TenantId WHERE TenantId = @Empty;
                UPDATE CarPhotos   SET TenantId = @TenantId WHERE TenantId = @Empty;
                UPDATE Events      SET TenantId = @TenantId WHERE TenantId = @Empty;
                UPDATE Bookings    SET TenantId = @TenantId WHERE TenantId = @Empty;
                UPDATE BookedDays  SET TenantId = @TenantId WHERE TenantId = @Empty;
                UPDATE DeviceTokens SET TenantId = @TenantId WHERE TenantId = @Empty;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Tenants");

            migrationBuilder.DropIndex(
                name: "UX_Users_Tenant_Email",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "DeviceTokens");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Cars");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "CarPhotos");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "BookedDays");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);
        }
    }
}
