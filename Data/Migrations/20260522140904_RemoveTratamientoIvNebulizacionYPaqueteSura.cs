using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IntranetPrueba.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveTratamientoIvNebulizacionYPaqueteSura : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PaqueteSura",
                table: "censo");

            migrationBuilder.DropColumn(
                name: "TratamientoFarmacologicoIvNebulizacion",
                table: "censo");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PaqueteSura",
                table: "censo",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TratamientoFarmacologicoIvNebulizacion",
                table: "censo",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);
        }
    }
}
