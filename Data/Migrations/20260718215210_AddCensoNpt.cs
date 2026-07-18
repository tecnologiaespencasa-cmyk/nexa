using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace IntranetPrueba.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCensoNpt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "censo_npt",
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
                    Barrio = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    MunicipioResidencia = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    ZonaDireccionSegunMunicipio = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ClasificacionZonaSura = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    TelefonoPrincipal = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    TelefonoAdicional1 = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    TelefonoAdicional2 = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    LlamadaBienvenida = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    TelefonoContacto = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    Observacion = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CodigoCie10 = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                    DiagnosticoDescriptivo = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    FechaValoracion = table.Column<DateTime>(type: "date", nullable: false),
                    ProgramaPertenece = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    AuxiliarEnfermeriaAsignado = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    TipoNutricion = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TipoSonda = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Picc = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    FechaUltimaCuracionPicc = table.Column<DateTime>(type: "date", nullable: true),
                    FechaInicioNpt = table.Column<DateTime>(type: "date", nullable: true),
                    FechaFinNpt = table.Column<DateTime>(type: "date", nullable: true),
                    DiasTratamiento = table.Column<int>(type: "integer", nullable: true),
                    HoraConexion = table.Column<TimeSpan>(type: "time without time zone", nullable: true),
                    HoraDesconexion = table.Column<TimeSpan>(type: "time without time zone", nullable: true),
                    CargueLaboratorios = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    CargueGlucometria = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    CargueServiciosComplementarios = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    CargueSeguimientoMedico = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    EquipoComodato = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    DescripcionEquipo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    NumeroPlacaEquipos = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    FechaEntregaEquipo = table.Column<DateTime>(type: "date", nullable: true),
                    FechaDevolucionEquipo = table.Column<DateTime>(type: "date", nullable: true),
                    FechaHospitalizacion = table.Column<DateTime>(type: "date", nullable: true),
                    MotivoHospitalizacion = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    RemitidoPorHospitalizacion = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    IpsIntramural = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    FechaPrimerSeguimiento24Horas = table.Column<DateTime>(type: "date", nullable: true),
                    FechaSegundoSeguimiento48Horas = table.Column<DateTime>(type: "date", nullable: true),
                    FechaTercerSeguimiento72Horas = table.Column<DateTime>(type: "date", nullable: true),
                    FechaCuartoSeguimientoSemana1 = table.Column<DateTime>(type: "date", nullable: true),
                    FechaQuintoSeguimientoSemana2 = table.Column<DateTime>(type: "date", nullable: true),
                    FechaSextoSeguimientoSemana3 = table.Column<DateTime>(type: "date", nullable: true),
                    FechaSeptimoSeguimientoSemana4 = table.Column<DateTime>(type: "date", nullable: true),
                    FechaAltaHospitalizacion = table.Column<DateTime>(type: "date", nullable: true),
                    FechaNovedadDevolucionProductos = table.Column<DateTime>(type: "date", nullable: true),
                    MotivoNovedadDevolucionProductos = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    NotificacionAuxiliarDevolucionProductos = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    FechaMaximaDevolucionProductos = table.Column<DateTime>(type: "date", nullable: true),
                    EstadoDevolucionServicioFarmaceutico = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    MotivoEgreso = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    FechaEgreso = table.Column<DateTime>(type: "date", nullable: true),
                    Estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_censo_npt", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_censo_npt_CreatedAtUtc",
                table: "censo_npt",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_censo_npt_FechaIngresoPrograma",
                table: "censo_npt",
                column: "FechaIngresoPrograma");

            migrationBuilder.CreateIndex(
                name: "IX_censo_npt_NumeroIdentificacion",
                table: "censo_npt",
                column: "NumeroIdentificacion");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "censo_npt");
        }
    }
}
