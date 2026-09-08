using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nexa.Data.Migrations
{
    /// <summary>
    /// Censo de crónicos, sección "Programas y servicios": se retiran "Clínica de heridas",
    /// "Estado en clínica de heridas", "Programa de nutrición (NE/NPT)" con sus tres campos
    /// dependientes (fecha de inicio, auxiliar asignado y fecha fin), y se agrega
    /// "Requiere cuidador". "Educación y plan de cuidados / enfermería" se conserva.
    ///
    /// OJO: el andamiaje de EF propuso RENOMBRAR ProgramaNutricion a RequiereCuidador,
    /// porque las dos son varchar(2). Eso habría dejado los 69 valores del programa de
    /// nutrición leyéndose como la respuesta a "requiere cuidador". Es la segunda vez que
    /// EF hace este emparejamiento en el mismo día; se reescribió a mano a seis DropColumn
    /// más un AddColumn nulo.
    ///
    /// Respaldo de lo que se borra:
    /// C:\tmp-nexa\respaldo-cronicos\campos_eliminados_cronicos.{json,csv} (69 filas).
    /// </summary>
    public partial class CronicosCampoRequiereCuidador : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ClinicaHeridas",
                table: "censo_cronicos");

            migrationBuilder.DropColumn(
                name: "EstadoClinicaHeridas",
                table: "censo_cronicos");

            migrationBuilder.DropColumn(
                name: "ProgramaNutricion",
                table: "censo_cronicos");

            migrationBuilder.DropColumn(
                name: "FechaInicioNutricion",
                table: "censo_cronicos");

            migrationBuilder.DropColumn(
                name: "AuxiliarAsignadoNutricion",
                table: "censo_cronicos");

            migrationBuilder.DropColumn(
                name: "FechaFinNutricion",
                table: "censo_cronicos");

            // Nace nula: las atenciones anteriores no respondieron esta pregunta.
            migrationBuilder.AddColumn<string>(
                name: "RequiereCuidador",
                table: "censo_cronicos",
                type: "character varying(2)",
                maxLength: 2,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Se devuelve la estructura, no el contenido: para recuperar los datos hay que
            // cargar el respaldo.
            migrationBuilder.DropColumn(
                name: "RequiereCuidador",
                table: "censo_cronicos");

            migrationBuilder.AddColumn<string>(
                name: "ClinicaHeridas",
                table: "censo_cronicos",
                type: "character varying(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EstadoClinicaHeridas",
                table: "censo_cronicos",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProgramaNutricion",
                table: "censo_cronicos",
                type: "character varying(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaInicioNutricion",
                table: "censo_cronicos",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AuxiliarAsignadoNutricion",
                table: "censo_cronicos",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaFinNutricion",
                table: "censo_cronicos",
                type: "date",
                nullable: true);
        }
    }
}
