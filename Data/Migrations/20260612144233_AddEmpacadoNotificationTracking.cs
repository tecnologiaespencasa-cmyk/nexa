using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IntranetPrueba.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEmpacadoNotificationTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "FarmaciaNotif24hRestanteUtc",
                table: "censo",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FarmaciaNotifAuxiliarUltimaUtc",
                table: "censo",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FarmaciaNotif24hRestanteUtc",
                table: "censo");

            migrationBuilder.DropColumn(
                name: "FarmaciaNotifAuxiliarUltimaUtc",
                table: "censo");
        }
    }
}
