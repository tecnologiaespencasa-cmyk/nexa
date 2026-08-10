using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nexa.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCensoClinicaHeridasManejoHerida : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DescripcionHerida",
                table: "censo_clinica_heridas",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FrecuenciaVisitasSemana",
                table: "censo_clinica_heridas",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UbicacionHerida",
                table: "censo_clinica_heridas",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DescripcionHerida",
                table: "censo_clinica_heridas");

            migrationBuilder.DropColumn(
                name: "FrecuenciaVisitasSemana",
                table: "censo_clinica_heridas");

            migrationBuilder.DropColumn(
                name: "UbicacionHerida",
                table: "censo_clinica_heridas");
        }
    }
}
