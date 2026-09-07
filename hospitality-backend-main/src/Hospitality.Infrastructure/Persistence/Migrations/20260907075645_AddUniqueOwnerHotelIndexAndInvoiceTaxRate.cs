using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hospitality.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueOwnerHotelIndexAndInvoiceTaxRate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "TaxRate",
                table: "Invoices",
                type: "numeric",
                nullable: false,
                defaultValue: 16m);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_HotelId",
                table: "AspNetUsers",
                column: "HotelId",
                unique: true,
                filter: "\"HotelId\" IS NOT NULL AND NOT \"IsDeleted\"");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_HotelId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "TaxRate",
                table: "Invoices");
        }
    }
}
