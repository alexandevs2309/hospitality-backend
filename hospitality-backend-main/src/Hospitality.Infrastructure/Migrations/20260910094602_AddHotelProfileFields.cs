using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hospitality.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddHotelProfileFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BusinessName",
                table: "Hotels",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CheckInTime",
                table: "Hotels",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CheckOutTime",
                table: "Hotels",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "Hotels",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HotelLanguages",
                table: "Hotels",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PostalCode",
                table: "Hotels",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SelectedModules",
                table: "Hotels",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TaxRate",
                table: "Hotels",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "YearOpened",
                table: "Hotels",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BusinessName",
                table: "Hotels");

            migrationBuilder.DropColumn(
                name: "CheckInTime",
                table: "Hotels");

            migrationBuilder.DropColumn(
                name: "CheckOutTime",
                table: "Hotels");

            migrationBuilder.DropColumn(
                name: "Currency",
                table: "Hotels");

            migrationBuilder.DropColumn(
                name: "HotelLanguages",
                table: "Hotels");

            migrationBuilder.DropColumn(
                name: "PostalCode",
                table: "Hotels");

            migrationBuilder.DropColumn(
                name: "SelectedModules",
                table: "Hotels");

            migrationBuilder.DropColumn(
                name: "TaxRate",
                table: "Hotels");

            migrationBuilder.DropColumn(
                name: "YearOpened",
                table: "Hotels");
        }
    }
}
