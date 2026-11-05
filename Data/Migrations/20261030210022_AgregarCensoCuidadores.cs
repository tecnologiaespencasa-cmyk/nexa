using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Nexa.Data.Migrations
{
    /// <inheritdoc />
    public partial class AgregarCensoCuidadores : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "censo_cuidadores",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CensoPacienteId = table.Column<long>(type: "bigint", nullable: true),
                    TipoIdentificacion = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    NumeroIdentificacion = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    NombrePaciente = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    FechaReporteEps = table.Column<DateTime>(type: "date", nullable: false),
                    FechaInicioAtencion = table.Column<DateTime>(type: "date", nullable: true),
                    NombreResponsable = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ParentescoResponsable = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    TelefonoResponsable = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    DireccionResponsable = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    DireccionMismaDelPaciente = table.Column<bool>(type: "boolean", nullable: false),
                    TiempoCuidadorHorasDia = table.Column<int>(type: "integer", nullable: true),
                    DiasAtencion = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    Horario = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    EscalaBarthel = table.Column<int>(type: "integer", nullable: true),
                    FechaCertificacionDependencia = table.Column<DateTime>(type: "date", nullable: true),
                    TiempoOrdenamientoMeses = table.Column<int>(type: "integer", nullable: true),
                    FechaVencimientoCertificado = table.Column<DateTime>(type: "date", nullable: true),
                    Observacion = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    VerificacionDerechos = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    FechaVerificacionDerechos = table.Column<DateTime>(type: "date", nullable: true),
                    AuxiliarAsignado = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    FechaEgreso = table.Column<DateTime>(type: "date", nullable: true),
                    MotivoEgreso = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_censo_cuidadores", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_censo_cuidadores_CensoPacienteId",
                table: "censo_cuidadores",
                column: "CensoPacienteId");

            migrationBuilder.CreateIndex(
                name: "IX_censo_cuidadores_NumeroIdentificacion",
                table: "censo_cuidadores",
                column: "NumeroIdentificacion");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "censo_cuidadores");
        }
    }
}
