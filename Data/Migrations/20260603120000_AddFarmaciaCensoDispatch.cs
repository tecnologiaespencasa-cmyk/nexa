using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IntranetPrueba.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFarmaciaCensoDispatch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "FarmaciaEnviadoAtUtc",
                table: "censo",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FarmaciaKardexVistoAtUtc",
                table: "censo",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FarmaciaRequisicionVistoAtUtc",
                table: "censo",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_censo_FarmaciaEnviadoAtUtc",
                table: "censo",
                column: "FarmaciaEnviadoAtUtc");

            migrationBuilder.Sql("""
                INSERT INTO "Permissions" ("Code", "Description")
                SELECT 'SCREEN_FARMACIA', 'Farmacia'
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM "Permissions"
                    WHERE "Code" = 'SCREEN_FARMACIA'
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_censo_FarmaciaEnviadoAtUtc",
                table: "censo");

            migrationBuilder.DropColumn(
                name: "FarmaciaEnviadoAtUtc",
                table: "censo");

            migrationBuilder.DropColumn(
                name: "FarmaciaKardexVistoAtUtc",
                table: "censo");

            migrationBuilder.DropColumn(
                name: "FarmaciaRequisicionVistoAtUtc",
                table: "censo");

            migrationBuilder.Sql("""
                DELETE FROM "Permissions"
                WHERE "Code" = 'SCREEN_FARMACIA';
                """);
        }
    }
}
