using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IntranetPrueba.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveCensoNovedadDocumental : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DescripcionNovedadDocumentosPaciente",
                table: "censo");

            migrationBuilder.DropColumn(
                name: "FechaReporteNovedadDocumentos",
                table: "censo");

            migrationBuilder.DropColumn(
                name: "HoraGestionSolucionNovedadDocumentos",
                table: "censo");

            migrationBuilder.DropColumn(
                name: "HoraReporteNovedadDocumentos",
                table: "censo");

            migrationBuilder.DropColumn(
                name: "PresentaNovedadAutorizacion",
                table: "censo");

            migrationBuilder.DropColumn(
                name: "PresentaNovedadKardex",
                table: "censo");

            migrationBuilder.DropColumn(
                name: "PresentaNovedadRequisicion",
                table: "censo");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DescripcionNovedadDocumentosPaciente",
                table: "censo",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaReporteNovedadDocumentos",
                table: "censo",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<TimeSpan>(
                name: "HoraGestionSolucionNovedadDocumentos",
                table: "censo",
                type: "time without time zone",
                nullable: true);

            migrationBuilder.AddColumn<TimeSpan>(
                name: "HoraReporteNovedadDocumentos",
                table: "censo",
                type: "time without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PresentaNovedadAutorizacion",
                table: "censo",
                type: "character varying(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PresentaNovedadKardex",
                table: "censo",
                type: "character varying(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PresentaNovedadRequisicion",
                table: "censo",
                type: "character varying(2)",
                maxLength: 2,
                nullable: true);
        }
    }
}
