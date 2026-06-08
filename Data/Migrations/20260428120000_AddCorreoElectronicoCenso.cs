using IntranetPrueba.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IntranetPrueba.Data.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260428120000_AddCorreoElectronicoCenso")]
    public partial class AddCorreoElectronicoCenso : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CorreoElectronico",
                table: "censo",
                type: "character varying(150)",
                maxLength: 150,
                nullable: false,
                defaultValue: string.Empty);

            migrationBuilder.AlterColumn<string>(
                name: "Telefono2",
                table: "censo",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: string.Empty,
                oldClrType: typeof(string),
                oldType: "character varying(10)",
                oldMaxLength: 10,
                oldNullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Telefono2",
                table: "censo",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(10)",
                oldMaxLength: 10);

            migrationBuilder.DropColumn(
                name: "CorreoElectronico",
                table: "censo");
        }
    }
}
