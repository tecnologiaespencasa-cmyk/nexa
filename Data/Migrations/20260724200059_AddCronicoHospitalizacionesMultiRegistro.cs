using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace IntranetPrueba.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCronicoHospitalizacionesMultiRegistro : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FechaAltaHospitalizacion",
                table: "censo_cronicos");

            migrationBuilder.DropColumn(
                name: "FechaCuartoSeguimientoSemana1",
                table: "censo_cronicos");

            migrationBuilder.DropColumn(
                name: "FechaHospitalizacion",
                table: "censo_cronicos");

            migrationBuilder.DropColumn(
                name: "FechaPrimerSeguimiento24Horas",
                table: "censo_cronicos");

            migrationBuilder.DropColumn(
                name: "FechaQuintoSeguimientoSemana2",
                table: "censo_cronicos");

            migrationBuilder.DropColumn(
                name: "FechaSegundoSeguimiento48Horas",
                table: "censo_cronicos");

            migrationBuilder.DropColumn(
                name: "FechaSeptimoSeguimientoSemana4",
                table: "censo_cronicos");

            migrationBuilder.DropColumn(
                name: "FechaSextoSeguimientoSemana3",
                table: "censo_cronicos");

            migrationBuilder.DropColumn(
                name: "FechaTercerSeguimiento72Horas",
                table: "censo_cronicos");

            migrationBuilder.DropColumn(
                name: "IpsIntramural",
                table: "censo_cronicos");

            migrationBuilder.DropColumn(
                name: "MotivoHospitalizacion",
                table: "censo_cronicos");

            migrationBuilder.DropColumn(
                name: "NumeroHospitalizacionesUltimoAnio",
                table: "censo_cronicos");

            migrationBuilder.DropColumn(
                name: "RemitidoPor",
                table: "censo_cronicos");

            migrationBuilder.CreateTable(
                name: "censo_cronico_hospitalizaciones",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CensoCronicoRecordId = table.Column<long>(type: "bigint", nullable: false),
                    Numero = table.Column<int>(type: "integer", nullable: false),
                    HospitalizacionJson = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_censo_cronico_hospitalizaciones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_censo_cronico_hospitalizaciones_censo_cronicos_CensoCronico~",
                        column: x => x.CensoCronicoRecordId,
                        principalTable: "censo_cronicos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_censo_cronico_hospitalizaciones_CensoCronicoRecordId_Numero",
                table: "censo_cronico_hospitalizaciones",
                columns: new[] { "CensoCronicoRecordId", "Numero" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "censo_cronico_hospitalizaciones");

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaAltaHospitalizacion",
                table: "censo_cronicos",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaCuartoSeguimientoSemana1",
                table: "censo_cronicos",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaHospitalizacion",
                table: "censo_cronicos",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaPrimerSeguimiento24Horas",
                table: "censo_cronicos",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaQuintoSeguimientoSemana2",
                table: "censo_cronicos",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaSegundoSeguimiento48Horas",
                table: "censo_cronicos",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaSeptimoSeguimientoSemana4",
                table: "censo_cronicos",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaSextoSeguimientoSemana3",
                table: "censo_cronicos",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaTercerSeguimiento72Horas",
                table: "censo_cronicos",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IpsIntramural",
                table: "censo_cronicos",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MotivoHospitalizacion",
                table: "censo_cronicos",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "NumeroHospitalizacionesUltimoAnio",
                table: "censo_cronicos",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RemitidoPor",
                table: "censo_cronicos",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);
        }
    }
}
