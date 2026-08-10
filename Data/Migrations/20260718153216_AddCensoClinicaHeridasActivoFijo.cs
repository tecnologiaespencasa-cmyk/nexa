using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nexa.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCensoClinicaHeridasActivoFijo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EquipoComodato",
                table: "censo_clinica_heridas",
                type: "character varying(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaDevolucionEquipo",
                table: "censo_clinica_heridas",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaEntregaEquipo",
                table: "censo_clinica_heridas",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NumeroPlacaEquipos",
                table: "censo_clinica_heridas",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EquipoComodato",
                table: "censo_clinica_heridas");

            migrationBuilder.DropColumn(
                name: "FechaDevolucionEquipo",
                table: "censo_clinica_heridas");

            migrationBuilder.DropColumn(
                name: "FechaEntregaEquipo",
                table: "censo_clinica_heridas");

            migrationBuilder.DropColumn(
                name: "NumeroPlacaEquipos",
                table: "censo_clinica_heridas");
        }
    }
}
