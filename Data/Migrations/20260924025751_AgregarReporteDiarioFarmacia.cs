using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Nexa.Data.Migrations
{
    /// <inheritdoc />
    public partial class AgregarReporteDiarioFarmacia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "farmacia_reporte_diario_envios",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Dia = table.Column<DateTime>(type: "date", nullable: false),
                    DesdeUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    HastaUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Intentos = table.Column<int>(type: "integer", nullable: false),
                    Despachos = table.Column<int>(type: "integer", nullable: false),
                    Destinatarios = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Error = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Instancia = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreadoAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IntentoAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EnviadoAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_farmacia_reporte_diario_envios", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_farmacia_reporte_diario_envios_Dia",
                table: "farmacia_reporte_diario_envios",
                column: "Dia",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "farmacia_reporte_diario_envios");
        }
    }
}
