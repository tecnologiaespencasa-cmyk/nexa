using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nexa.Data.Migrations
{
    /// <inheritdoc />
    public partial class PersistCensoAddressValidation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AsumirDireccionErrada",
                table: "censo",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "DireccionValidada",
                table: "censo",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Las direcciones históricas ya fueron aceptadas al guardar su atención.
            migrationBuilder.Sql(
                """
                UPDATE censo
                SET "DireccionValidada" = TRUE
                WHERE COALESCE("Direccion", '') <> '';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AsumirDireccionErrada",
                table: "censo");

            migrationBuilder.DropColumn(
                name: "DireccionValidada",
                table: "censo");
        }
    }
}
