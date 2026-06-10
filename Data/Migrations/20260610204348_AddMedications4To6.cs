using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IntranetPrueba.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMedications4To6 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DiasMedicamento4",
                table: "censo",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DiasMedicamento5",
                table: "censo",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DiasMedicamento6",
                table: "censo",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DosisMedicamento4",
                table: "censo",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DosisMedicamento5",
                table: "censo",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DosisMedicamento6",
                table: "censo",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FrecuenciaAdministracionMedicamento4",
                table: "censo",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FrecuenciaAdministracionMedicamento5",
                table: "censo",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FrecuenciaAdministracionMedicamento6",
                table: "censo",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MedidaMedicamento4",
                table: "censo",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MedidaMedicamento5",
                table: "censo",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MedidaMedicamento6",
                table: "censo",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NombreMedicamentoNumero4",
                table: "censo",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NombreMedicamentoNumero5",
                table: "censo",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NombreMedicamentoNumero6",
                table: "censo",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NumeroDosisMedicamento4",
                table: "censo",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NumeroDosisMedicamento5",
                table: "censo",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NumeroDosisMedicamento6",
                table: "censo",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ViaAdministracionMedicamento4",
                table: "censo",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ViaAdministracionMedicamento5",
                table: "censo",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ViaAdministracionMedicamento6",
                table: "censo",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DiasMedicamento4",
                table: "censo");

            migrationBuilder.DropColumn(
                name: "DiasMedicamento5",
                table: "censo");

            migrationBuilder.DropColumn(
                name: "DiasMedicamento6",
                table: "censo");

            migrationBuilder.DropColumn(
                name: "DosisMedicamento4",
                table: "censo");

            migrationBuilder.DropColumn(
                name: "DosisMedicamento5",
                table: "censo");

            migrationBuilder.DropColumn(
                name: "DosisMedicamento6",
                table: "censo");

            migrationBuilder.DropColumn(
                name: "FrecuenciaAdministracionMedicamento4",
                table: "censo");

            migrationBuilder.DropColumn(
                name: "FrecuenciaAdministracionMedicamento5",
                table: "censo");

            migrationBuilder.DropColumn(
                name: "FrecuenciaAdministracionMedicamento6",
                table: "censo");

            migrationBuilder.DropColumn(
                name: "MedidaMedicamento4",
                table: "censo");

            migrationBuilder.DropColumn(
                name: "MedidaMedicamento5",
                table: "censo");

            migrationBuilder.DropColumn(
                name: "MedidaMedicamento6",
                table: "censo");

            migrationBuilder.DropColumn(
                name: "NombreMedicamentoNumero4",
                table: "censo");

            migrationBuilder.DropColumn(
                name: "NombreMedicamentoNumero5",
                table: "censo");

            migrationBuilder.DropColumn(
                name: "NombreMedicamentoNumero6",
                table: "censo");

            migrationBuilder.DropColumn(
                name: "NumeroDosisMedicamento4",
                table: "censo");

            migrationBuilder.DropColumn(
                name: "NumeroDosisMedicamento5",
                table: "censo");

            migrationBuilder.DropColumn(
                name: "NumeroDosisMedicamento6",
                table: "censo");

            migrationBuilder.DropColumn(
                name: "ViaAdministracionMedicamento4",
                table: "censo");

            migrationBuilder.DropColumn(
                name: "ViaAdministracionMedicamento5",
                table: "censo");

            migrationBuilder.DropColumn(
                name: "ViaAdministracionMedicamento6",
                table: "censo");
        }
    }
}
