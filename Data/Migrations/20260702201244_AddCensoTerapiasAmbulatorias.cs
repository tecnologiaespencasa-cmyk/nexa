using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace IntranetPrueba.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCensoTerapiasAmbulatorias : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "censo_terapias_ambulatorias",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    NombrePaciente = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TipoIdentificacion = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    NumeroIdentificacion = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    FechaNacimiento = table.Column<DateTime>(type: "date", nullable: false),
                    Edad = table.Column<int>(type: "integer", nullable: false),
                    CorreoElectronico = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Cantidad = table.Column<int>(type: "integer", nullable: false),
                    FrecuenciaTerapia = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TipoTerapia = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CodigoCie10 = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                    DiagnosticoDescriptivo = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    NumeroAutorizacion = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Direccion = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    DireccionValidada = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    AsumirDireccionErrada = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DetalleDireccion = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ClasificacionZonaSura = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    MunicipioResidencia = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Barrio = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ZonaDireccionSegunMunicipio = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Area = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    IpsQueRemite = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    VistoBuenoRangoFueraAnexo = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    TelefonoPrincipal = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    TelefonoAdicional1 = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    TelefonoAdicional2 = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    Fisioterapeuta = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    EstadoGestion = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    FechaInicio = table.Column<DateTime>(type: "date", nullable: false),
                    FechaFin = table.Column<DateTime>(type: "date", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_censo_terapias_ambulatorias", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_censo_terapias_ambulatorias_CreatedAtUtc",
                table: "censo_terapias_ambulatorias",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_censo_terapias_ambulatorias_FechaInicio",
                table: "censo_terapias_ambulatorias",
                column: "FechaInicio");

            migrationBuilder.CreateIndex(
                name: "IX_censo_terapias_ambulatorias_NumeroIdentificacion",
                table: "censo_terapias_ambulatorias",
                column: "NumeroIdentificacion");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "censo_terapias_ambulatorias");
        }
    }
}
