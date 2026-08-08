using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FleetRental.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCarPricingModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "DailyRate",
                table: "Cars",
                newName: "Rate");

            migrationBuilder.AddColumn<string>(
                name: "PricingModel",
                table: "Cars",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "PerDay");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PricingModel",
                table: "Cars");

            migrationBuilder.RenameColumn(
                name: "Rate",
                table: "Cars",
                newName: "DailyRate");
        }
    }
}
