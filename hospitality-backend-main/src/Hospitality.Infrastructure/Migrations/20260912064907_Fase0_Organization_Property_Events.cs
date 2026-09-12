using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hospitality.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Fase0_Organization_Property_Events : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "Hotels",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DomainEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AggregateType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    AggregateId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    Payload = table.Column<string>(type: "jsonb", nullable: true),
                    OccurredOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ActorUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    CorrelationId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DomainEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Organizations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    BusinessName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    TaxId = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    PhoneNumber = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Plan = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SelectedModules = table.Column<string>(type: "jsonb", nullable: true),
                    DefaultCurrency = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    TimeZone = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Organizations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OutboxMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DomainEventId = table.Column<Guid>(type: "uuid", nullable: true),
                    Topic = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Payload = table.Column<string>(type: "jsonb", nullable: true),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    NextAttemptAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxMessages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PropertyAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    PropertyRole = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PropertyAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PropertyAssignments_Hotels_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Hotels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrganizationMembers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    OrganizationRole = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganizationMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrganizationMembers_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // ── M2: Backfill idempotente de datos (Organización → Propiedad) ──
            // 1) Org por cada hotel existente (Id de la org = Id del hotel, mapeo 1:1
            //    que permite multi-propiedad futura sin romper datos actuales).
            migrationBuilder.Sql("""
                INSERT INTO "Organizations"
                    ("Id", "Name", "BusinessName", "TaxId", "Email", "PhoneNumber", "Plan",
                     "SelectedModules", "DefaultCurrency", "TimeZone", "IsActive", "IsDeleted",
                     "DeletedAt", "CreatedAt", "UpdatedAt")
                SELECT h."Id", h."Name", h."BusinessName", NULL, h."Email", h."PhoneNumber", 'small',
                       CASE WHEN h."SelectedModules" IS NULL THEN NULL
                            ELSE to_jsonb(string_to_array(h."SelectedModules", ',')) END,
                       COALESCE(h."Currency", 'USD'), COALESCE(h."TimeZone", 'UTC'),
                       h."IsActive", h."IsDeleted", h."DeletedAt", NOW(), NOW()
                FROM "Hotels" h
                WHERE NOT EXISTS (SELECT 1 FROM "Organizations" o WHERE o."Id" = h."Id");
                """);

            // 2) Vincular cada hotel a su organización.
            migrationBuilder.Sql("""
                UPDATE "Hotels" SET "OrganizationId" = "Id"
                WHERE "OrganizationId" IS NULL OR "OrganizationId" = '00000000-0000-0000-0000-000000000000';
                """);

            // 3) Member org (Owner) para cada propietario (vía AspNetUsers.HotelId legacy).
            migrationBuilder.Sql("""
                INSERT INTO "OrganizationMembers"
                    ("Id", "OrganizationId", "UserId", "OrganizationRole", "IsActive",
                     "IsDeleted", "DeletedAt", "CreatedAt", "UpdatedAt")
                SELECT gen_random_uuid(), u."HotelId", u."Id", 'Owner', true, false, NULL, NOW(), NOW()
                FROM "AspNetUsers" u
                WHERE u."HotelId" IS NOT NULL AND NOT u."IsDeleted"
                  AND NOT EXISTS (SELECT 1 FROM "OrganizationMembers" om
                                  WHERE om."OrganizationId" = u."HotelId" AND om."UserId" = u."Id");
                """);

            // 4) PropertyAssignment (Owner) para el mismo propietario.
            migrationBuilder.Sql("""
                INSERT INTO "PropertyAssignments"
                    ("Id", "PropertyId", "UserId", "PropertyRole", "IsActive",
                     "IsDeleted", "DeletedAt", "CreatedAt", "UpdatedAt")
                SELECT gen_random_uuid(), u."HotelId", u."Id", 'Owner', true, false, NULL, NOW(), NOW()
                FROM "AspNetUsers" u
                WHERE u."HotelId" IS NOT NULL AND NOT u."IsDeleted"
                  AND NOT EXISTS (SELECT 1 FROM "PropertyAssignments" pa
                                  WHERE pa."PropertyId" = u."HotelId" AND pa."UserId" = u."Id");
                """);

            // 5) Aplicar NOT NULL después del backfill.
            migrationBuilder.AlterColumn<Guid>(
                name: "OrganizationId",
                table: "Hotels",
                type: "uuid",
                nullable: false);

            migrationBuilder.CreateIndex(
                name: "IX_Hotels_OrganizationId",
                table: "Hotels",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_DomainEvents_AggregateType_AggregateId",
                table: "DomainEvents",
                columns: new[] { "AggregateType", "AggregateId" });

            migrationBuilder.CreateIndex(
                name: "IX_DomainEvents_AggregateType_AggregateId_Version",
                table: "DomainEvents",
                columns: new[] { "AggregateType", "AggregateId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DomainEvents_OccurredOn",
                table: "DomainEvents",
                column: "OccurredOn");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationMembers_OrganizationId_UserId",
                table: "OrganizationMembers",
                columns: new[] { "OrganizationId", "UserId" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationMembers_UserId",
                table: "OrganizationMembers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Organizations_IsActive_IsDeleted",
                table: "Organizations",
                columns: new[] { "IsActive", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_Organizations_Name",
                table: "Organizations",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_Status_NextAttemptAt",
                table: "OutboxMessages",
                columns: new[] { "Status", "NextAttemptAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PropertyAssignments_PropertyId",
                table: "PropertyAssignments",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_PropertyAssignments_PropertyId_UserId",
                table: "PropertyAssignments",
                columns: new[] { "PropertyId", "UserId" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_PropertyAssignments_UserId",
                table: "PropertyAssignments",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Hotels_Organizations_OrganizationId",
                table: "Hotels",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Hotels_Organizations_OrganizationId",
                table: "Hotels");

            migrationBuilder.DropTable(
                name: "DomainEvents");

            migrationBuilder.DropTable(
                name: "OrganizationMembers");

            migrationBuilder.DropTable(
                name: "OutboxMessages");

            migrationBuilder.DropTable(
                name: "PropertyAssignments");

            migrationBuilder.DropTable(
                name: "Organizations");

            migrationBuilder.DropIndex(
                name: "IX_Hotels_OrganizationId",
                table: "Hotels");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "Hotels");
        }
    }
}
