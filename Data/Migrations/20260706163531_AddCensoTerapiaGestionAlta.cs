using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nexa.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCensoTerapiaGestionAlta : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AltaNotificacionEnviadaAtUtc",
                table: "censo_terapias_ambulatorias",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EstadoAlta",
                table: "censo_terapias_ambulatorias",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Activo");

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaAlta",
                table: "censo_terapias_ambulatorias",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MotivoAlta",
                table: "censo_terapias_ambulatorias",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_censo_terapias_ambulatorias_EstadoAlta",
                table: "censo_terapias_ambulatorias",
                column: "EstadoAlta");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_censo_terapias_ambulatorias_EstadoAlta",
                table: "censo_terapias_ambulatorias");

            migrationBuilder.DropColumn(
                name: "AltaNotificacionEnviadaAtUtc",
                table: "censo_terapias_ambulatorias");

            migrationBuilder.DropColumn(
                name: "EstadoAlta",
                table: "censo_terapias_ambulatorias");

            migrationBuilder.DropColumn(
                name: "FechaAlta",
                table: "censo_terapias_ambulatorias");

            migrationBuilder.DropColumn(
                name: "MotivoAlta",
                table: "censo_terapias_ambulatorias");
        }
    }
}
