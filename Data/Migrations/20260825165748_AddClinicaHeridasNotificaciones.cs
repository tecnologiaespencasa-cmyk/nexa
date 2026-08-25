using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nexa.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddClinicaHeridasNotificaciones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "FarmaciaNotif24hRestanteUtc",
                table: "censo_clinica_heridas_kardex",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FarmaciaNotifAuxiliarUltimaUtc",
                table: "censo_clinica_heridas_kardex",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FarmaciaNotif24hRestanteUtc",
                table: "censo_clinica_heridas_kardex");

            migrationBuilder.DropColumn(
                name: "FarmaciaNotifAuxiliarUltimaUtc",
                table: "censo_clinica_heridas_kardex");
        }
    }
}
