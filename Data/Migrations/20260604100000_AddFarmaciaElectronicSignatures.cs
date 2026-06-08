using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IntranetPrueba.Data.Migrations
{
    public partial class AddFarmaciaElectronicSignatures : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FarmaciaNombreRecibe",
                table: "censo",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FarmaciaFirmaEntregaDataUrl",
                table: "censo",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FarmaciaFirmaRecibeDataUrl",
                table: "censo",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FarmaciaFechaHoraRecepcionUtc",
                table: "censo",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FarmaciaFirmaActualizadaAtUtc",
                table: "censo",
                type: "timestamp with time zone",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FarmaciaNombreRecibe",
                table: "censo");

            migrationBuilder.DropColumn(
                name: "FarmaciaFirmaEntregaDataUrl",
                table: "censo");

            migrationBuilder.DropColumn(
                name: "FarmaciaFirmaRecibeDataUrl",
                table: "censo");

            migrationBuilder.DropColumn(
                name: "FarmaciaFechaHoraRecepcionUtc",
                table: "censo");

            migrationBuilder.DropColumn(
                name: "FarmaciaFirmaActualizadaAtUtc",
                table: "censo");
        }
    }
}
