using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Nexa.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCronicoKardexFarmacia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "FarmaciaEnviadoAtUtc",
                table: "censo_cronico_agudizaciones",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FarmaciaEstado",
                table: "censo_cronico_agudizaciones",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Nuevo");

            migrationBuilder.AddColumn<DateTime>(
                name: "FarmaciaKardexVistoAtUtc",
                table: "censo_cronico_agudizaciones",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "FarmaciaOkKardex",
                table: "censo_cronico_agudizaciones",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "FarmaciaRequisicionVistoAtUtc",
                table: "censo_cronico_agudizaciones",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "KardexCerradoAtUtc",
                table: "censo_cronico_agudizaciones",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "KardexEdicionJson",
                table: "censo_cronico_agudizaciones",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReaperturaAprobadaPor",
                table: "censo_cronico_agudizaciones",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReaperturaSolicitadaPor",
                table: "censo_cronico_agudizaciones",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequisicionFarmaciaJson",
                table: "censo_cronico_agudizaciones",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "TuvoReaperturaKardex",
                table: "censo_cronico_agudizaciones",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "censo_cronico_kardex_reaperturas",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CensoCronicoAgudizacionId = table.Column<long>(type: "bigint", nullable: false),
                    Motivo = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SolicitadoPorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SolicitadoPorNombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SolicitadoAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ResueltoPorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResueltoPorNombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ResueltoAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ObservacionResolucion = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_censo_cronico_kardex_reaperturas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_censo_cronico_kardex_reaperturas_censo_cronico_agudizacione~",
                        column: x => x.CensoCronicoAgudizacionId,
                        principalTable: "censo_cronico_agudizaciones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_censo_cronico_agudizaciones_FarmaciaEnviadoAtUtc",
                table: "censo_cronico_agudizaciones",
                column: "FarmaciaEnviadoAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_censo_cronico_kardex_reaperturas_CensoCronicoAgudizacionId_~",
                table: "censo_cronico_kardex_reaperturas",
                columns: new[] { "CensoCronicoAgudizacionId", "Estado" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "censo_cronico_kardex_reaperturas");

            migrationBuilder.DropIndex(
                name: "IX_censo_cronico_agudizaciones_FarmaciaEnviadoAtUtc",
                table: "censo_cronico_agudizaciones");

            migrationBuilder.DropColumn(
                name: "FarmaciaEnviadoAtUtc",
                table: "censo_cronico_agudizaciones");

            migrationBuilder.DropColumn(
                name: "FarmaciaEstado",
                table: "censo_cronico_agudizaciones");

            migrationBuilder.DropColumn(
                name: "FarmaciaKardexVistoAtUtc",
                table: "censo_cronico_agudizaciones");

            migrationBuilder.DropColumn(
                name: "FarmaciaOkKardex",
                table: "censo_cronico_agudizaciones");

            migrationBuilder.DropColumn(
                name: "FarmaciaRequisicionVistoAtUtc",
                table: "censo_cronico_agudizaciones");

            migrationBuilder.DropColumn(
                name: "KardexCerradoAtUtc",
                table: "censo_cronico_agudizaciones");

            migrationBuilder.DropColumn(
                name: "KardexEdicionJson",
                table: "censo_cronico_agudizaciones");

            migrationBuilder.DropColumn(
                name: "ReaperturaAprobadaPor",
                table: "censo_cronico_agudizaciones");

            migrationBuilder.DropColumn(
                name: "ReaperturaSolicitadaPor",
                table: "censo_cronico_agudizaciones");

            migrationBuilder.DropColumn(
                name: "RequisicionFarmaciaJson",
                table: "censo_cronico_agudizaciones");

            migrationBuilder.DropColumn(
                name: "TuvoReaperturaKardex",
                table: "censo_cronico_agudizaciones");
        }
    }
}
