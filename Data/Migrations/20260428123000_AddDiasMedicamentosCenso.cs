using IntranetPrueba.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IntranetPrueba.Data.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260428123000_AddDiasMedicamentosCenso")]
    public partial class AddDiasMedicamentosCenso : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DiasMedicamentoPrincipal",
                table: "censo",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DiasMedicamento2",
                table: "censo",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DiasMedicamento3",
                table: "censo",
                type: "integer",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DiasMedicamentoPrincipal",
                table: "censo");

            migrationBuilder.DropColumn(
                name: "DiasMedicamento2",
                table: "censo");

            migrationBuilder.DropColumn(
                name: "DiasMedicamento3",
                table: "censo");
        }
    }
}
