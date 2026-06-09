using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IntranetPrueba.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFarmaciaEstadoCampos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FarmaciaCantidadEntregas",
                table: "censo",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FarmaciaEmpacadoAtUtc",
                table: "censo",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FarmaciaEntregaActual",
                table: "censo",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "FarmaciaEsEntregaParcial",
                table: "censo",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FarmaciaEstado",
                table: "censo",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "FarmaciaFacturado",
                table: "censo",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "FarmaciaOkKardex",
                table: "censo",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Inicializar FarmaciaEntregaActual en 1 para todos los registros
            migrationBuilder.Sql(@"UPDATE censo SET ""FarmaciaEntregaActual"" = 1;");

            // Migrar registros existentes al estado apropiado segun datos previos
            migrationBuilder.Sql(@"
                UPDATE censo SET ""FarmaciaEstado"" = 'Despachado'
                WHERE ""FarmaciaEnviadoAtUtc"" IS NOT NULL
                  AND ""FarmaciaNombreRecibe"" IS NOT NULL AND ""FarmaciaNombreRecibe"" <> ''
                  AND ""FarmaciaFirmaEntregaDataUrl"" IS NOT NULL
                  AND ""FarmaciaFirmaRecibeDataUrl"" IS NOT NULL
                  AND ""FarmaciaFechaHoraRecepcionUtc"" IS NOT NULL;

                UPDATE censo SET ""FarmaciaEstado"" = 'Recepcionado'
                WHERE ""FarmaciaEnviadoAtUtc"" IS NOT NULL
                  AND ""FarmaciaEstado"" = ''
                  AND (""FarmaciaKardexVistoAtUtc"" IS NOT NULL OR ""FarmaciaRequisicionVistoAtUtc"" IS NOT NULL);

                UPDATE censo SET ""FarmaciaEstado"" = 'Nuevo'
                WHERE ""FarmaciaEnviadoAtUtc"" IS NOT NULL
                  AND ""FarmaciaEstado"" = '';
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FarmaciaCantidadEntregas",
                table: "censo");

            migrationBuilder.DropColumn(
                name: "FarmaciaEmpacadoAtUtc",
                table: "censo");

            migrationBuilder.DropColumn(
                name: "FarmaciaEntregaActual",
                table: "censo");

            migrationBuilder.DropColumn(
                name: "FarmaciaEsEntregaParcial",
                table: "censo");

            migrationBuilder.DropColumn(
                name: "FarmaciaEstado",
                table: "censo");

            migrationBuilder.DropColumn(
                name: "FarmaciaFacturado",
                table: "censo");

            migrationBuilder.DropColumn(
                name: "FarmaciaOkKardex",
                table: "censo");
        }
    }
}
