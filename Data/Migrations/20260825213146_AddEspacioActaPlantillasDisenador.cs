using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Nexa.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEspacioActaPlantillasDisenador : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FirmasJson",
                table: "espacio_actas_documentales",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "espacio_acta_plantillas",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Codigo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Descripcion = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    Icono = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    TituloActa = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CamposJson = table.Column<string>(type: "text", nullable: false),
                    BloquesJson = table.Column<string>(type: "text", nullable: false),
                    FirmasJson = table.Column<string>(type: "text", nullable: false),
                    NumerarTitulos = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CampoNombre = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    CampoDocumento = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    CampoCorreo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    CampoUsuario = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    Activa = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    Eliminada = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    CreadaPorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreadaPorNombre = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    CreadaAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ActualizadaPorNombre = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    ActualizadaAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_espacio_acta_plantillas", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_espacio_acta_plantillas_Codigo",
                table: "espacio_acta_plantillas",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_espacio_acta_plantillas_Eliminada_Activa",
                table: "espacio_acta_plantillas",
                columns: new[] { "Eliminada", "Activa" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "espacio_acta_plantillas");

            migrationBuilder.DropColumn(
                name: "FirmasJson",
                table: "espacio_actas_documentales");
        }
    }
}
