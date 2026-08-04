using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace IntranetPrueba.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEspacioActasFirmas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "espacio_activo_actas",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EspacioActivoId = table.Column<long>(type: "bigint", nullable: false),
                    Tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    EntregaPorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    EntregaPorNombre = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    EntregaPorCargo = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    FirmaEntregaDataUrl = table.Column<string>(type: "text", nullable: false),
                    RecibePorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    RecibePorNombre = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    RecibePorDocumento = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    FirmaRecibeDataUrl = table.Column<string>(type: "text", nullable: false),
                    Observaciones = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    EquipoDescripcion = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Serial = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    CodigoActivo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    Especificaciones = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    FirmadaAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_espacio_activo_actas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_espacio_activo_actas_espacio_activos_EspacioActivoId",
                        column: x => x.EspacioActivoId,
                        principalTable: "espacio_activos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "espacio_firmas_usuario",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    FirmaDataUrl = table.Column<string>(type: "text", nullable: false),
                    NombreFirmante = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    Cargo = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    ActualizadaAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_espacio_firmas_usuario", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_espacio_firmas_usuario_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_espacio_activo_actas_EspacioActivoId_FirmadaAtUtc",
                table: "espacio_activo_actas",
                columns: new[] { "EspacioActivoId", "FirmadaAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "espacio_activo_actas");

            migrationBuilder.DropTable(
                name: "espacio_firmas_usuario");
        }
    }
}
