using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Nexa.Data.Migrations
{
    /// <inheritdoc />
    public partial class AgregarTelevisor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "GestionAnalistaAtUtc",
                table: "censo",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GestionAnalistaPor",
                table: "censo",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "GestionCompletaAtUtc",
                table: "censo",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GestionCompletaPor",
                table: "censo",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "televisor_cortes",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Dia = table.Column<DateTime>(type: "date", nullable: false),
                    Tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    EstadoAlUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    GeneradoAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Recuperado = table.Column<bool>(type: "boolean", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    DatosJson = table.Column<string>(type: "jsonb", nullable: false),
                    Instancia = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_televisor_cortes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "televisor_turnos_ajuste",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Dia = table.Column<DateTime>(type: "date", nullable: false),
                    NursingAssistantId = table.Column<int>(type: "integer", nullable: false),
                    Turno = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    AjustadoPor = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    AjustadoAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_televisor_turnos_ajuste", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_televisor_cortes_Dia_Tipo",
                table: "televisor_cortes",
                columns: new[] { "Dia", "Tipo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_televisor_turnos_ajuste_Dia_NursingAssistantId",
                table: "televisor_turnos_ajuste",
                columns: new[] { "Dia", "NursingAssistantId" },
                unique: true);

            // El permiso del Televisor se crea aquí (y no solo en DataSeeder) para poder asignarlo en
            // el mismo paso a una única persona, como se pidió el 2026-10-01: Emmanuel Estrada
            // (EESTRADA). Los demás lo reciben desde Administración > Permisos.
            migrationBuilder.Sql("""
                INSERT INTO "Permissions" ("Code", "Description")
                SELECT 'SCREEN_TELEVISOR', 'Televisor - Indicadores asistenciales'
                WHERE NOT EXISTS (SELECT 1 FROM "Permissions" WHERE upper("Code") = 'SCREEN_TELEVISOR');

                INSERT INTO "UserPermissions" ("UserId", "PermissionId", "GrantedAtUtc")
                SELECT u."Id", p."Id", now()
                FROM "Users" u
                CROSS JOIN "Permissions" p
                WHERE u."NormalizedUsername" = 'EESTRADA'
                  AND p."Code" = 'SCREEN_TELEVISOR'
                  AND NOT EXISTS (
                      SELECT 1 FROM "UserPermissions" up
                      WHERE up."UserId" = u."Id" AND up."PermissionId" = p."Id");
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM "UserPermissions"
                WHERE "PermissionId" IN (SELECT "Id" FROM "Permissions" WHERE "Code" = 'SCREEN_TELEVISOR');
                DELETE FROM "Permissions" WHERE "Code" = 'SCREEN_TELEVISOR';
                """);

            migrationBuilder.DropTable(
                name: "televisor_cortes");

            migrationBuilder.DropTable(
                name: "televisor_turnos_ajuste");

            migrationBuilder.DropColumn(
                name: "GestionAnalistaAtUtc",
                table: "censo");

            migrationBuilder.DropColumn(
                name: "GestionAnalistaPor",
                table: "censo");

            migrationBuilder.DropColumn(
                name: "GestionCompletaAtUtc",
                table: "censo");

            migrationBuilder.DropColumn(
                name: "GestionCompletaPor",
                table: "censo");
        }
    }
}
