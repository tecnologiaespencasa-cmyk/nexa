using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Nexa.Data.Migrations
{
    /// <summary>
    /// La requisición de insumos de NPT pasa de clínica de heridas a su propio censo.
    ///
    /// En heridas era un tipo más de kardex, activado con un "Sí" en Dispositivos, porque ahí vivía
    /// la maquinaria de requisiciones. Pertenece al programa de NPT, así que se lleva allá: tabla
    /// propia (censo_npt_kardex, una requisición por registro), adjuntos propios y carril propio en
    /// la bandeja de farmacia, con el mismo ciclo de siempre.
    ///
    /// El traslado no arrastra datos porque no hay ninguno: al 2026-09-11 había **0 requisiciones
    /// de NPT** creadas y **ningún paciente** de heridas con NPT en "Sí" (los 9 diligenciados decían
    /// "No"). Por eso la columna se puede retirar sin migrar nada.
    ///
    /// Respaldo de las 9 filas antes de borrar la columna:
    /// C:\tmp-nexa\respaldo-heridas-npt\campo_npt_clinica_heridas.{json,csv}
    /// </summary>
    public partial class TrasladarRequisicionNptASuCenso : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Npt",
                table: "censo_clinica_heridas");

            migrationBuilder.CreateTable(
                name: "censo_npt_kardex",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CensoNptRecordId = table.Column<long>(type: "bigint", nullable: false),
                    KardexJson = table.Column<string>(type: "text", nullable: true),
                    ElaboradoPor = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    FarmaciaEnviadoAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FarmaciaEstado = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    FarmaciaOkKardex = table.Column<bool>(type: "boolean", nullable: false),
                    FarmaciaKardexVistoAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FarmaciaRequisicionVistoAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FarmaciaEsEntregaParcial = table.Column<bool>(type: "boolean", nullable: true),
                    FarmaciaCantidadEntregas = table.Column<int>(type: "integer", nullable: true),
                    FarmaciaEntregaActual = table.Column<int>(type: "integer", nullable: false),
                    FarmaciaFacturado = table.Column<bool>(type: "boolean", nullable: false),
                    FarmaciaEmpacadoAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FarmaciaBolsaDesempacada = table.Column<bool>(type: "boolean", nullable: false),
                    FarmaciaNombreRecibe = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    FarmaciaFirmaEntregaDataUrl = table.Column<string>(type: "text", nullable: true),
                    FarmaciaFirmaRecibeDataUrl = table.Column<string>(type: "text", nullable: true),
                    FarmaciaFechaHoraRecepcionUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FarmaciaFirmaActualizadaAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FarmaciaNotifAuxiliarUltimaUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FarmaciaNotif24hRestanteUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    KardexCerradoAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_censo_npt_kardex", x => x.Id);
                    table.ForeignKey(
                        name: "FK_censo_npt_kardex_censo_npt_CensoNptRecordId",
                        column: x => x.CensoNptRecordId,
                        principalTable: "censo_npt",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "censo_npt_kardex_adjuntos",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CensoNptKardexId = table.Column<long>(type: "bigint", nullable: false),
                    FileName = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    FileData = table.Column<byte[]>(type: "bytea", nullable: false),
                    UploadedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_censo_npt_kardex_adjuntos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_censo_npt_kardex_adjuntos_censo_npt_kardex_CensoNptKardexId",
                        column: x => x.CensoNptKardexId,
                        principalTable: "censo_npt_kardex",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_censo_npt_kardex_CensoNptRecordId",
                table: "censo_npt_kardex",
                column: "CensoNptRecordId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_censo_npt_kardex_FarmaciaEnviadoAtUtc",
                table: "censo_npt_kardex",
                column: "FarmaciaEnviadoAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_censo_npt_kardex_adjuntos_CensoNptKardexId",
                table: "censo_npt_kardex_adjuntos",
                column: "CensoNptKardexId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "censo_npt_kardex_adjuntos");

            migrationBuilder.DropTable(
                name: "censo_npt_kardex");

            migrationBuilder.AddColumn<string>(
                name: "Npt",
                table: "censo_clinica_heridas",
                type: "character varying(2)",
                maxLength: 2,
                nullable: true);
        }
    }
}
