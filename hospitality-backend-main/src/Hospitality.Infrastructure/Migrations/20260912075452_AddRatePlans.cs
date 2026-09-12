using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hospitality.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRatePlans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "RatePlanId",
                table: "RoomTypes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PenaltyAmount",
                table: "Reservations",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "RatePlanId",
                table: "Reservations",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RatePlans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Multiplier = table.Column<decimal>(type: "numeric", nullable: false),
                    MinStay = table.Column<int>(type: "integer", nullable: false),
                    Refundability = table.Column<int>(type: "integer", nullable: false),
                    CancellationDeadlineHours = table.Column<int>(type: "integer", nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    HotelId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RatePlans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RatePlans_Hotels_HotelId",
                        column: x => x.HotelId,
                        principalTable: "Hotels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RoomTypes_RatePlanId",
                table: "RoomTypes",
                column: "RatePlanId");

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_RatePlanId",
                table: "Reservations",
                column: "RatePlanId");

            migrationBuilder.CreateIndex(
                name: "IX_RatePlans_HotelId_Name",
                table: "RatePlans",
                columns: new[] { "HotelId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RatePlans_IsDefault",
                table: "RatePlans",
                column: "IsDefault");

            migrationBuilder.AddForeignKey(
                name: "FK_Reservations_RatePlans_RatePlanId",
                table: "Reservations",
                column: "RatePlanId",
                principalTable: "RatePlans",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_RoomTypes_RatePlans_RatePlanId",
                table: "RoomTypes",
                column: "RatePlanId",
                principalTable: "RatePlans",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Reservations_RatePlans_RatePlanId",
                table: "Reservations");

            migrationBuilder.DropForeignKey(
                name: "FK_RoomTypes_RatePlans_RatePlanId",
                table: "RoomTypes");

            migrationBuilder.DropTable(
                name: "RatePlans");

            migrationBuilder.DropIndex(
                name: "IX_RoomTypes_RatePlanId",
                table: "RoomTypes");

            migrationBuilder.DropIndex(
                name: "IX_Reservations_RatePlanId",
                table: "Reservations");

            migrationBuilder.DropColumn(
                name: "RatePlanId",
                table: "RoomTypes");

            migrationBuilder.DropColumn(
                name: "PenaltyAmount",
                table: "Reservations");

            migrationBuilder.DropColumn(
                name: "RatePlanId",
                table: "Reservations");
        }
    }
}
