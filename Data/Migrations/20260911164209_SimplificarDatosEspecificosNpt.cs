using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nexa.Data.Migrations
{
    /// <summary>
    /// Censo de NPT, sección "Datos específicos" / "Información clínica": se retiran "Programa al
    /// que pertenece", "Tipo de nutrición" y "Tipo de sonda". El código CIE10 y el diagnóstico
    /// —que ya existían como columnas, ocultos y copiados del maestro— pasan a ser propios de la
    /// atención (ver AplicarMaestroAModeloNpt), con la misma lógica que el maestro: catálogo
    /// general, no el listado cerrado de clínica de heridas.
    ///
    /// Respaldo de lo que se borra: C:\tmp-nexa\respaldo-npt\campos_eliminados_npt.{json,csv}
    /// (1 fila al 2026-09-11).
    /// </summary>
    public partial class SimplificarDatosEspecificosNpt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProgramaPertenece",
                table: "censo_npt");

            migrationBuilder.DropColumn(
                name: "TipoNutricion",
                table: "censo_npt");

            migrationBuilder.DropColumn(
                name: "TipoSonda",
                table: "censo_npt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ProgramaPertenece",
                table: "censo_npt",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TipoNutricion",
                table: "censo_npt",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TipoSonda",
                table: "censo_npt",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");
        }
    }
}
