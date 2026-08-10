using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nexa.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCensoReaperturaMarcas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ReaperturaAprobadaPor",
                table: "censo",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReaperturaSolicitadaPor",
                table: "censo",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "TuvoReaperturaKardex",
                table: "censo",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReaperturaAprobadaPor",
                table: "censo");

            migrationBuilder.DropColumn(
                name: "ReaperturaSolicitadaPor",
                table: "censo");

            migrationBuilder.DropColumn(
                name: "TuvoReaperturaKardex",
                table: "censo");
        }
    }
}
