using IntranetPrueba.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IntranetPrueba.Data.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260428124500_RemoveDiasEstanciaCenso")]
    public partial class RemoveDiasEstanciaCenso : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DiasEstancia",
                table: "censo");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DiasEstancia",
                table: "censo",
                type: "integer",
                nullable: true);
        }
    }
}
