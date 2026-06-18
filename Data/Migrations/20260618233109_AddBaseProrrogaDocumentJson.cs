using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IntranetPrueba.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBaseProrrogaDocumentJson : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ProrrogaKardexEdicionJson",
                table: "censo",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProrrogaRequisicionFarmaciaJson",
                table: "censo",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProrrogaKardexEdicionJson",
                table: "censo");

            migrationBuilder.DropColumn(
                name: "ProrrogaRequisicionFarmaciaJson",
                table: "censo");
        }
    }
}
