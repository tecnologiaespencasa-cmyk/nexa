using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Nexa.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCensoPacienteMaestro : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "CensoPacienteId",
                table: "censo_terapias_ambulatorias",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "CensoPacienteId",
                table: "censo_npt",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "CensoPacienteId",
                table: "censo_cronicos",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "CensoPacienteId",
                table: "censo_clinica_heridas",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "CensoPacienteId",
                table: "censo",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "censo_paciente",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FechaIngreso = table.Column<DateTime>(type: "date", nullable: false),
                    HoraIngreso = table.Column<TimeSpan>(type: "time without time zone", nullable: false),
                    FechaRespuesta = table.Column<DateTime>(type: "date", nullable: true),
                    HoraRespuesta = table.Column<TimeSpan>(type: "time without time zone", nullable: true),
                    IndicadorTiempoRespuestaMinutos = table.Column<int>(type: "integer", nullable: true),
                    NombreRecepcionaCaso = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    NombreRealizaKardex = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    TipoIdentificacion = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    NumeroIdentificacion = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    NombrePaciente = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    FechaNacimiento = table.Column<DateTime>(type: "date", nullable: false),
                    Edad = table.Column<int>(type: "integer", nullable: false),
                    Genero = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    CorreoElectronico = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    CodigoCie10 = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: true),
                    DiagnosticoDescriptivo = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Asegurador = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Direccion = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    DireccionValidada = table.Column<bool>(type: "boolean", nullable: false),
                    AsumirDireccionErrada = table.Column<bool>(type: "boolean", nullable: false),
                    DetalleDireccion = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ClasificacionZonaSura = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    MunicipioResidencia = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Barrio = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    ZonaDireccionSegunMunicipio = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Area = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    IpsQueRemite = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    VistoBuenoRangoFueraAnexo = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    Telefono1 = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    Telefono2 = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    Telefono3 = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreadoPor = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ActualizadoPor = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_censo_paciente", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "censo_paciente_programa",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CensoPacienteId = table.Column<long>(type: "bigint", nullable: false),
                    Programa = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    RegistroId = table.Column<long>(type: "bigint", nullable: true),
                    AgregadoAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AgregadoPor = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CerradoAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CerradoPor = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    MotivoCierre = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_censo_paciente_programa", x => x.Id);
                    table.ForeignKey(
                        name: "FK_censo_paciente_programa_censo_paciente_CensoPacienteId",
                        column: x => x.CensoPacienteId,
                        principalTable: "censo_paciente",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_censo_terapias_ambulatorias_CensoPacienteId",
                table: "censo_terapias_ambulatorias",
                column: "CensoPacienteId");

            migrationBuilder.CreateIndex(
                name: "IX_censo_npt_CensoPacienteId",
                table: "censo_npt",
                column: "CensoPacienteId");

            migrationBuilder.CreateIndex(
                name: "IX_censo_cronicos_CensoPacienteId",
                table: "censo_cronicos",
                column: "CensoPacienteId");

            migrationBuilder.CreateIndex(
                name: "IX_censo_clinica_heridas_CensoPacienteId",
                table: "censo_clinica_heridas",
                column: "CensoPacienteId");

            migrationBuilder.CreateIndex(
                name: "IX_censo_CensoPacienteId",
                table: "censo",
                column: "CensoPacienteId");

            migrationBuilder.CreateIndex(
                name: "IX_censo_paciente_FechaIngreso",
                table: "censo_paciente",
                column: "FechaIngreso");

            migrationBuilder.CreateIndex(
                name: "IX_censo_paciente_NombrePaciente",
                table: "censo_paciente",
                column: "NombrePaciente");

            migrationBuilder.CreateIndex(
                name: "IX_censo_paciente_NumeroIdentificacion",
                table: "censo_paciente",
                column: "NumeroIdentificacion",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_censo_paciente_programa_abierto",
                table: "censo_paciente_programa",
                columns: new[] { "CensoPacienteId", "Programa" },
                filter: "\"CerradoAtUtc\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_censo_paciente_programa_Programa_RegistroId",
                table: "censo_paciente_programa",
                columns: new[] { "Programa", "RegistroId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "censo_paciente_programa");

            migrationBuilder.DropTable(
                name: "censo_paciente");

            migrationBuilder.DropIndex(
                name: "IX_censo_terapias_ambulatorias_CensoPacienteId",
                table: "censo_terapias_ambulatorias");

            migrationBuilder.DropIndex(
                name: "IX_censo_npt_CensoPacienteId",
                table: "censo_npt");

            migrationBuilder.DropIndex(
                name: "IX_censo_cronicos_CensoPacienteId",
                table: "censo_cronicos");

            migrationBuilder.DropIndex(
                name: "IX_censo_clinica_heridas_CensoPacienteId",
                table: "censo_clinica_heridas");

            migrationBuilder.DropIndex(
                name: "IX_censo_CensoPacienteId",
                table: "censo");

            migrationBuilder.DropColumn(
                name: "CensoPacienteId",
                table: "censo_terapias_ambulatorias");

            migrationBuilder.DropColumn(
                name: "CensoPacienteId",
                table: "censo_npt");

            migrationBuilder.DropColumn(
                name: "CensoPacienteId",
                table: "censo_cronicos");

            migrationBuilder.DropColumn(
                name: "CensoPacienteId",
                table: "censo_clinica_heridas");

            migrationBuilder.DropColumn(
                name: "CensoPacienteId",
                table: "censo");
        }
    }
}
