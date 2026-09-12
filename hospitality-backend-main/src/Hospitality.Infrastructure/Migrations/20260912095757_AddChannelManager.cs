using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hospitality.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddChannelManager : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CredentialsJson",
                table: "Channels",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ChannelMappings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ChannelId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoomTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChannelRoomCode = table.Column<string>(type: "text", nullable: false),
                    ChannelRatePlanCode = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    ChannelId1 = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChannelMappings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChannelMappings_Channels_ChannelId",
                        column: x => x.ChannelId,
                        principalTable: "Channels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ChannelMappings_Channels_ChannelId1",
                        column: x => x.ChannelId1,
                        principalTable: "Channels",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ChannelMappings_RoomTypes_RoomTypeId",
                        column: x => x.RoomTypeId,
                        principalTable: "RoomTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChannelMappings_ChannelId_RoomTypeId",
                table: "ChannelMappings",
                columns: new[] { "ChannelId", "RoomTypeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChannelMappings_ChannelId1",
                table: "ChannelMappings",
                column: "ChannelId1");

            migrationBuilder.CreateIndex(
                name: "IX_ChannelMappings_RoomTypeId",
                table: "ChannelMappings",
                column: "RoomTypeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChannelMappings");

            migrationBuilder.DropColumn(
                name: "CredentialsJson",
                table: "Channels");
        }
    }
}
