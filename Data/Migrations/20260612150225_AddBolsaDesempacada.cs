using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IntranetPrueba.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBolsaDesempacada : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "FarmaciaBolsaDesempacada",
                table: "censo",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FarmaciaBolsaDesempacada",
                table: "censo");
        }
    }
}
