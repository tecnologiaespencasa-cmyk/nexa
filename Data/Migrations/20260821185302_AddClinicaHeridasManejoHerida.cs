using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nexa.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddClinicaHeridasManejoHerida : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Vac",
                table: "censo_clinica_heridas",
                type: "character varying(2)",
                maxLength: 2,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(2)",
                oldMaxLength: 2);

            migrationBuilder.AlterColumn<string>(
                name: "Picc",
                table: "censo_clinica_heridas",
                type: "character varying(2)",
                maxLength: 2,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(2)",
                oldMaxLength: 2);

            migrationBuilder.AddColumn<string>(
                name: "ApositoMedicamento1",
                table: "censo_clinica_heridas",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApositoMedicamento2",
                table: "censo_clinica_heridas",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApositoMedicamento3",
                table: "censo_clinica_heridas",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApositoMedicamento4",
                table: "censo_clinica_heridas",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DuracionTratamientoDias",
                table: "censo_clinica_heridas",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FrecuenciaVisita",
                table: "censo_clinica_heridas",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ApositoMedicamento1",
                table: "censo_clinica_heridas");

            migrationBuilder.DropColumn(
                name: "ApositoMedicamento2",
                table: "censo_clinica_heridas");

            migrationBuilder.DropColumn(
                name: "ApositoMedicamento3",
                table: "censo_clinica_heridas");

            migrationBuilder.DropColumn(
                name: "ApositoMedicamento4",
                table: "censo_clinica_heridas");

            migrationBuilder.DropColumn(
                name: "DuracionTratamientoDias",
                table: "censo_clinica_heridas");

            migrationBuilder.DropColumn(
                name: "FrecuenciaVisita",
                table: "censo_clinica_heridas");

            migrationBuilder.AlterColumn<string>(
                name: "Vac",
                table: "censo_clinica_heridas",
                type: "character varying(2)",
                maxLength: 2,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(2)",
                oldMaxLength: 2,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Picc",
                table: "censo_clinica_heridas",
                type: "character varying(2)",
                maxLength: 2,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(2)",
                oldMaxLength: 2,
                oldNullable: true);
        }
    }
}
