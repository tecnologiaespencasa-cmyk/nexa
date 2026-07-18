using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace IntranetPrueba.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCensoClinicaHeridas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "censo_clinica_heridas",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Asegurador = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    FechaIngresoPrograma = table.Column<DateTime>(type: "date", nullable: false),
                    TipoIdentificacion = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    NumeroIdentificacion = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    NombrePaciente = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    FechaNacimiento = table.Column<DateTime>(type: "date", nullable: false),
                    Edad = table.Column<int>(type: "integer", nullable: false),
                    Genero = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Direccion = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    DireccionValidada = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    AsumirDireccionErrada = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DetalleDireccion = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ClasificacionZonaSura = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    MunicipioResidencia = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Barrio = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    ZonaDireccionSegunMunicipio = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    TelefonoPrincipal = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    TelefonoAdicional1 = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    TelefonoAdicional2 = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    LlamadaBienvenida = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    TelefonoContacto = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    Observacion = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CodigoCie10 = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                    DiagnosticoDescriptivo = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    FechaValoracion = table.Column<DateTime>(type: "date", nullable: false),
                    ProgramaPertenece = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    AuxiliarEnfermeriaAsignado = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Picc = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    Vac = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_censo_clinica_heridas", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_censo_clinica_heridas_CreatedAtUtc",
                table: "censo_clinica_heridas",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_censo_clinica_heridas_FechaIngresoPrograma",
                table: "censo_clinica_heridas",
                column: "FechaIngresoPrograma");

            migrationBuilder.CreateIndex(
                name: "IX_censo_clinica_heridas_NumeroIdentificacion",
                table: "censo_clinica_heridas",
                column: "NumeroIdentificacion");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "censo_clinica_heridas");
        }
    }
}
