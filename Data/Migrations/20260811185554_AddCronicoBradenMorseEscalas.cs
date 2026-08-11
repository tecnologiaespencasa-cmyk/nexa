using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nexa.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCronicoBradenMorseEscalas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // El campo "Braden" era texto libre (hasta 50 caracteres) y pasa a ser numérico.
            // Se convierte solo el contenido que ya es un entero válido; el resto queda NULL
            // en lugar de romper la migración con un error de cast de Postgres.
            migrationBuilder.Sql(
                "ALTER TABLE censo_cronicos ALTER COLUMN \"Braden\" TYPE integer " +
                "USING (CASE WHEN \"Braden\" ~ '^\\s*-?\\d+\\s*$' THEN trim(\"Braden\")::integer ELSE NULL END);");

            migrationBuilder.AddColumn<int>(
                name: "EscalaMorse",
                table: "censo_cronicos",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EscalaMorse",
                table: "censo_cronicos");

            migrationBuilder.AlterColumn<string>(
                name: "Braden",
                table: "censo_cronicos",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);
        }
    }
}
