using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nexa.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddClinicaHeridasKardexDespacho : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "FarmaciaBolsaDesempacada",
                table: "censo_clinica_heridas_kardex",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "FarmaciaCantidadEntregas",
                table: "censo_clinica_heridas_kardex",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FarmaciaEmpacadoAtUtc",
                table: "censo_clinica_heridas_kardex",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FarmaciaEntregaActual",
                table: "censo_clinica_heridas_kardex",
                type: "integer",
                nullable: false,
                // La primera entrega es la 1: con 0 las requisiciones que ya existian arrancarian
                // fuera de rango al configurar entrega parcial.
                defaultValue: 1);

            migrationBuilder.AddColumn<bool>(
                name: "FarmaciaEsEntregaParcial",
                table: "censo_clinica_heridas_kardex",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "FarmaciaFacturado",
                table: "censo_clinica_heridas_kardex",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "FarmaciaFechaHoraRecepcionUtc",
                table: "censo_clinica_heridas_kardex",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FarmaciaFirmaActualizadaAtUtc",
                table: "censo_clinica_heridas_kardex",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FarmaciaFirmaEntregaDataUrl",
                table: "censo_clinica_heridas_kardex",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FarmaciaFirmaRecibeDataUrl",
                table: "censo_clinica_heridas_kardex",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FarmaciaNombreRecibe",
                table: "censo_clinica_heridas_kardex",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FarmaciaRequisicionVistoAtUtc",
                table: "censo_clinica_heridas_kardex",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FarmaciaBolsaDesempacada",
                table: "censo_clinica_heridas_kardex");

            migrationBuilder.DropColumn(
                name: "FarmaciaCantidadEntregas",
                table: "censo_clinica_heridas_kardex");

            migrationBuilder.DropColumn(
                name: "FarmaciaEmpacadoAtUtc",
                table: "censo_clinica_heridas_kardex");

            migrationBuilder.DropColumn(
                name: "FarmaciaEntregaActual",
                table: "censo_clinica_heridas_kardex");

            migrationBuilder.DropColumn(
                name: "FarmaciaEsEntregaParcial",
                table: "censo_clinica_heridas_kardex");

            migrationBuilder.DropColumn(
                name: "FarmaciaFacturado",
                table: "censo_clinica_heridas_kardex");

            migrationBuilder.DropColumn(
                name: "FarmaciaFechaHoraRecepcionUtc",
                table: "censo_clinica_heridas_kardex");

            migrationBuilder.DropColumn(
                name: "FarmaciaFirmaActualizadaAtUtc",
                table: "censo_clinica_heridas_kardex");

            migrationBuilder.DropColumn(
                name: "FarmaciaFirmaEntregaDataUrl",
                table: "censo_clinica_heridas_kardex");

            migrationBuilder.DropColumn(
                name: "FarmaciaFirmaRecibeDataUrl",
                table: "censo_clinica_heridas_kardex");

            migrationBuilder.DropColumn(
                name: "FarmaciaNombreRecibe",
                table: "censo_clinica_heridas_kardex");

            migrationBuilder.DropColumn(
                name: "FarmaciaRequisicionVistoAtUtc",
                table: "censo_clinica_heridas_kardex");
        }
    }
}
