using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Nexa.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEspacioActasDocumentales : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "espacio_actas_documentales",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PlantillaCodigo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    PlantillaNombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TituloActa = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    NombreRecibe = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    DocumentoRecibe = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    CorreoRecibe = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    UsuarioRecibe = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    ValoresJson = table.Column<string>(type: "text", nullable: false),
                    CuerpoHtml = table.Column<string>(type: "text", nullable: false),
                    EmitidaPorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    EmitidaPorNombre = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    EmitidaPorCargo = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    EmitidaPorDocumento = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    FirmaEmiteDataUrl = table.Column<string>(type: "text", nullable: false),
                    FirmaRecibeDataUrl = table.Column<string>(type: "text", nullable: false),
                    CorreoEnviado = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    CorreoEnviadoAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CorreoError = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    FirmadaAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_espacio_actas_documentales", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_espacio_actas_documentales_DocumentoRecibe",
                table: "espacio_actas_documentales",
                column: "DocumentoRecibe");

            migrationBuilder.CreateIndex(
                name: "IX_espacio_actas_documentales_FirmadaAtUtc",
                table: "espacio_actas_documentales",
                column: "FirmadaAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_espacio_actas_documentales_PlantillaCodigo",
                table: "espacio_actas_documentales",
                column: "PlantillaCodigo");

            migrationBuilder.CreateIndex(
                name: "IX_espacio_actas_documentales_UsuarioRecibe",
                table: "espacio_actas_documentales",
                column: "UsuarioRecibe");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "espacio_actas_documentales");
        }
    }
}
