using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Nexa.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCensoCronicos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "censo_cronicos",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FuenteIngreso = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    FechaIngreso = table.Column<DateTime>(type: "date", nullable: false),
                    TipoIdentificacion = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    NumeroIdentificacion = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Genero = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Direccion = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    DireccionValidada = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    AsumirDireccionErrada = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DetalleDireccion = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ClasificacionZonaSura = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    MunicipioResidencia = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Barrio = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    ZonaDireccionSegunMunicipio = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Area = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    ClasificacionCaso = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    EstadoPaciente = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    NumeroAgudizacionesUltimoAnio = table.Column<int>(type: "integer", nullable: true),
                    FechaAgudizacion = table.Column<DateTime>(type: "date", nullable: true),
                    MotivoAgudizacion = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    DescripcionAgudizacion = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    DetalleDescripcionCie10 = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    DiagnosticoCronicoCie10 = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: true),
                    GrupoPatologiaCronica = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    DiagnosticoCronicoComplementario = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: true),
                    GrupoPatologiaCronicaComplementario = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    BarthelAuditado = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    FechaAuditoria = table.Column<DateTime>(type: "date", nullable: true),
                    CalificacionBarthel = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Karnofsky = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Fast = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Rankin = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    DisneaMmrc = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Nyha = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Braden = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    RiesgoCaida = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    RiesgoLesionPiel = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ClinicaHeridas = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    EstadoClinicaHeridas = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ProgramaNutricion = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    FechaInicioNutricion = table.Column<DateTime>(type: "date", nullable: true),
                    AuxiliarAsignadoNutricion = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    FechaFinNutricion = table.Column<DateTime>(type: "date", nullable: true),
                    EducacionPlanCuidados = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    TerapiaFisica = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    TerapiaRespiratoria = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    TerapiaOcupacional = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    Fonoaudiologia = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    Nutricion = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    Psicologia = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    Traqueostomia = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    SondaNasogastrica = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    CalibreSondaNasogastrica = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    FrecuenciaCambioSondaNasogastrica = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    FechaUltimoCambioSondaNasogastrica = table.Column<DateTime>(type: "date", nullable: true),
                    SondaGastrostomia = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    Colostomia = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    SondaCistostomia = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    CateterPicc = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    SondaVesical = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    CalibreSondaVesical = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    FrecuenciaCambioSondaVesical = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    FechaUltimoCambioSondaVesical = table.Column<DateTime>(type: "date", nullable: true),
                    FechaProximoCambioSondaVesical = table.Column<DateTime>(type: "date", nullable: true),
                    ObservacionCambioSonda = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    FormulaControl = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    MipresPanales = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    TallaPanales = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: true),
                    FechaUltimaPrescripcionPanales = table.Column<DateTime>(type: "date", nullable: true),
                    TiempoPrescripcionPanalesMeses = table.Column<int>(type: "integer", nullable: true),
                    EstadoMipresPanales = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    MipresNutricion = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    FechaUltimaPrescripcionNutricion = table.Column<DateTime>(type: "date", nullable: true),
                    TiempoPrescripcionNutricionMeses = table.Column<int>(type: "integer", nullable: true),
                    EstadoMipresNutricion = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    FechaHospitalizacion = table.Column<DateTime>(type: "date", nullable: true),
                    MotivoHospitalizacion = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    RemitidoPor = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IpsIntramural = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    FechaPrimerSeguimiento24Horas = table.Column<DateTime>(type: "date", nullable: true),
                    FechaSegundoSeguimiento48Horas = table.Column<DateTime>(type: "date", nullable: true),
                    FechaTercerSeguimiento72Horas = table.Column<DateTime>(type: "date", nullable: true),
                    FechaCuartoSeguimientoSemana1 = table.Column<DateTime>(type: "date", nullable: true),
                    FechaQuintoSeguimientoSemana2 = table.Column<DateTime>(type: "date", nullable: true),
                    FechaSextoSeguimientoSemana3 = table.Column<DateTime>(type: "date", nullable: true),
                    FechaSeptimoSeguimientoSemana4 = table.Column<DateTime>(type: "date", nullable: true),
                    FechaAltaHospitalizacion = table.Column<DateTime>(type: "date", nullable: true),
                    NumeroHospitalizacionesUltimoAnio = table.Column<int>(type: "integer", nullable: true),
                    EgresaProgramaCronico = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    MotivoEgreso = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    FechaEgreso = table.Column<DateTime>(type: "date", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_censo_cronicos", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_censo_cronicos_CreatedAtUtc",
                table: "censo_cronicos",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_censo_cronicos_FechaIngreso",
                table: "censo_cronicos",
                column: "FechaIngreso");

            migrationBuilder.CreateIndex(
                name: "IX_censo_cronicos_NumeroIdentificacion",
                table: "censo_cronicos",
                column: "NumeroIdentificacion");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "censo_cronicos");
        }
    }
}
