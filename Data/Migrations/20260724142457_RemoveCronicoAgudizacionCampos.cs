using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IntranetPrueba.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveCronicoAgudizacionCampos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DescripcionAgudizacion",
                table: "censo_cronicos");

            migrationBuilder.DropColumn(
                name: "DetalleDescripcionCie10",
                table: "censo_cronicos");

            migrationBuilder.DropColumn(
                name: "FechaAgudizacion",
                table: "censo_cronicos");

            migrationBuilder.DropColumn(
                name: "MotivoAgudizacion",
                table: "censo_cronicos");

            migrationBuilder.DropColumn(
                name: "NumeroAgudizacionesUltimoAnio",
                table: "censo_cronicos");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DescripcionAgudizacion",
                table: "censo_cronicos",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DetalleDescripcionCie10",
                table: "censo_cronicos",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaAgudizacion",
                table: "censo_cronicos",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MotivoAgudizacion",
                table: "censo_cronicos",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "NumeroAgudizacionesUltimoAnio",
                table: "censo_cronicos",
                type: "integer",
                nullable: true);
        }
    }
}
