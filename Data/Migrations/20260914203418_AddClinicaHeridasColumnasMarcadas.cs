using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nexa.Data.Migrations
{
    /// <summary>
    /// Columna nueva para que farmacia marque en amarillo las fechas de aplicación ya entregadas
    /// de una requisición de clínica de heridas (JSON con los índices de columna marcados). No
    /// toca ninguna fila existente: nace vacía (NULL) en todo lo que ya había.
    /// </summary>
    public partial class AddClinicaHeridasColumnasMarcadas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FarmaciaColumnasMarcadasJson",
                table: "censo_clinica_heridas_kardex",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FarmaciaColumnasMarcadasJson",
                table: "censo_clinica_heridas_kardex");
        }
    }
}
