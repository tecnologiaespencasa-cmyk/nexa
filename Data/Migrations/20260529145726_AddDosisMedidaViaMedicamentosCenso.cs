using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IntranetPrueba.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDosisMedidaViaMedicamentosCenso : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "DosisMedicamento2",
                table: "censo",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DosisMedicamento3",
                table: "censo",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DosisMedicamentoPrincipal",
                table: "censo",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MedidaMedicamento2",
                table: "censo",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MedidaMedicamento3",
                table: "censo",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MedidaMedicamentoPrincipal",
                table: "censo",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ViaAdministracionMedicamento2",
                table: "censo",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ViaAdministracionMedicamento3",
                table: "censo",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ViaAdministracionMedicamentoPrincipal",
                table: "censo",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DosisMedicamento2",
                table: "censo");

            migrationBuilder.DropColumn(
                name: "DosisMedicamento3",
                table: "censo");

            migrationBuilder.DropColumn(
                name: "DosisMedicamentoPrincipal",
                table: "censo");

            migrationBuilder.DropColumn(
                name: "MedidaMedicamento2",
                table: "censo");

            migrationBuilder.DropColumn(
                name: "MedidaMedicamento3",
                table: "censo");

            migrationBuilder.DropColumn(
                name: "MedidaMedicamentoPrincipal",
                table: "censo");

            migrationBuilder.DropColumn(
                name: "ViaAdministracionMedicamento2",
                table: "censo");

            migrationBuilder.DropColumn(
                name: "ViaAdministracionMedicamento3",
                table: "censo");

            migrationBuilder.DropColumn(
                name: "ViaAdministracionMedicamentoPrincipal",
                table: "censo");
        }
    }
}
