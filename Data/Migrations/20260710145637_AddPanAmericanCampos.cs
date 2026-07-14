using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IntranetPrueba.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPanAmericanCampos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PanAmericanActivadorPoliza",
                table: "censo",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PanAmericanCartaAutorizacion",
                table: "censo",
                type: "character varying(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PanAmericanFechaCirugia",
                table: "censo",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PanAmericanFechaSolicitud",
                table: "censo",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PanAmericanIpsQuirurgica",
                table: "censo",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PanAmericanNombreCirujano",
                table: "censo",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PanAmericanNumeroAutorizacion",
                table: "censo",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PanAmericanProcedimiento",
                table: "censo",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PanAmericanActivadorPoliza",
                table: "censo");

            migrationBuilder.DropColumn(
                name: "PanAmericanCartaAutorizacion",
                table: "censo");

            migrationBuilder.DropColumn(
                name: "PanAmericanFechaCirugia",
                table: "censo");

            migrationBuilder.DropColumn(
                name: "PanAmericanFechaSolicitud",
                table: "censo");

            migrationBuilder.DropColumn(
                name: "PanAmericanIpsQuirurgica",
                table: "censo");

            migrationBuilder.DropColumn(
                name: "PanAmericanNombreCirujano",
                table: "censo");

            migrationBuilder.DropColumn(
                name: "PanAmericanNumeroAutorizacion",
                table: "censo");

            migrationBuilder.DropColumn(
                name: "PanAmericanProcedimiento",
                table: "censo");
        }
    }
}
