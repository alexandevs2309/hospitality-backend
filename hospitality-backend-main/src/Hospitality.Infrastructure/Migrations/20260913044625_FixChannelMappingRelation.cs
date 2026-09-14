using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hospitality.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixChannelMappingRelation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ChannelMappings_Channels_ChannelId1",
                table: "ChannelMappings");

            migrationBuilder.DropIndex(
                name: "IX_ChannelMappings_ChannelId1",
                table: "ChannelMappings");

            migrationBuilder.DropColumn(
                name: "ChannelId1",
                table: "ChannelMappings");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ChannelId1",
                table: "ChannelMappings",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChannelMappings_ChannelId1",
                table: "ChannelMappings",
                column: "ChannelId1");

            migrationBuilder.AddForeignKey(
                name: "FK_ChannelMappings_Channels_ChannelId1",
                table: "ChannelMappings",
                column: "ChannelId1",
                principalTable: "Channels",
                principalColumn: "Id");
        }
    }
}
