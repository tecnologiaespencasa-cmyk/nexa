using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nexa.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCronicoFarmaciaPipelineCompleto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "FarmaciaBolsaDesempacada",
                table: "censo_cronico_agudizaciones",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "FarmaciaCantidadEntregas",
                table: "censo_cronico_agudizaciones",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FarmaciaEmpacadoAtUtc",
                table: "censo_cronico_agudizaciones",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FarmaciaEntregaActual",
                table: "censo_cronico_agudizaciones",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<bool>(
                name: "FarmaciaEsEntregaParcial",
                table: "censo_cronico_agudizaciones",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "FarmaciaFacturado",
                table: "censo_cronico_agudizaciones",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "FarmaciaFechaHoraRecepcionUtc",
                table: "censo_cronico_agudizaciones",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FarmaciaFirmaActualizadaAtUtc",
                table: "censo_cronico_agudizaciones",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FarmaciaFirmaEntregaDataUrl",
                table: "censo_cronico_agudizaciones",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FarmaciaFirmaRecibeDataUrl",
                table: "censo_cronico_agudizaciones",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FarmaciaNombreRecibe",
                table: "censo_cronico_agudizaciones",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FarmaciaBolsaDesempacada",
                table: "censo_cronico_agudizaciones");

            migrationBuilder.DropColumn(
                name: "FarmaciaCantidadEntregas",
                table: "censo_cronico_agudizaciones");

            migrationBuilder.DropColumn(
                name: "FarmaciaEmpacadoAtUtc",
                table: "censo_cronico_agudizaciones");

            migrationBuilder.DropColumn(
                name: "FarmaciaEntregaActual",
                table: "censo_cronico_agudizaciones");

            migrationBuilder.DropColumn(
                name: "FarmaciaEsEntregaParcial",
                table: "censo_cronico_agudizaciones");

            migrationBuilder.DropColumn(
                name: "FarmaciaFacturado",
                table: "censo_cronico_agudizaciones");

            migrationBuilder.DropColumn(
                name: "FarmaciaFechaHoraRecepcionUtc",
                table: "censo_cronico_agudizaciones");

            migrationBuilder.DropColumn(
                name: "FarmaciaFirmaActualizadaAtUtc",
                table: "censo_cronico_agudizaciones");

            migrationBuilder.DropColumn(
                name: "FarmaciaFirmaEntregaDataUrl",
                table: "censo_cronico_agudizaciones");

            migrationBuilder.DropColumn(
                name: "FarmaciaFirmaRecibeDataUrl",
                table: "censo_cronico_agudizaciones");

            migrationBuilder.DropColumn(
                name: "FarmaciaNombreRecibe",
                table: "censo_cronico_agudizaciones");
        }
    }
}
