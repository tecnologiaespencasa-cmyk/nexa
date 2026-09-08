using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nexa.Data.Migrations
{
    /// <summary>
    /// Censo de agudos: se retiran "Programa", "Sistemas de presión negativa VAC" y
    /// "Clínica de heridas", y se agrega "Requiere cuidador".
    ///
    /// Los tres que salen quedaron sin sentido con el censo unificado: el programa del
    /// paciente lo dice ahora el carril de programas, y tanto el VAC como la clínica de
    /// heridas son estados del censo de clínica de heridas, no casillas de agudos.
    ///
    /// OJO: el andamiaje de EF propuso RENOMBRAR SistemasPresionNegativaVac a
    /// RequiereCuidador, porque las dos son varchar(2) y quedaron en posiciones
    /// parecidas. Eso habría dejado los 2973 valores de VAC leyéndose como si fueran la
    /// respuesta a "requiere cuidador". Se reescribió a mano: la columna vieja se borra
    /// y la nueva nace vacía, que es lo único cierto mientras nadie la diligencie.
    ///
    /// Respaldo de lo que se borra, por si alguna vez hiciera falta reconstruirlo:
    /// C:\tmp-nexa\respaldo-agudos\campos_eliminados_agudos.{json,csv} (2973 filas).
    /// </summary>
    public partial class AgudosCampoRequiereCuidador : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Programa",
                table: "censo");

            migrationBuilder.DropColumn(
                name: "SistemasPresionNegativaVac",
                table: "censo");

            migrationBuilder.DropColumn(
                name: "ClinicaHeridas",
                table: "censo");

            // Nace nula: las atenciones anteriores no respondieron esta pregunta, y
            // ponerles "No" por defecto sería inventar una respuesta que nadie dio.
            migrationBuilder.AddColumn<string>(
                name: "RequiereCuidador",
                table: "censo",
                type: "character varying(2)",
                maxLength: 2,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Se devuelve la estructura, no el contenido: los datos de las tres columnas
            // se pierden al aplicar Up. Para recuperarlos hay que cargar el respaldo.
            migrationBuilder.DropColumn(
                name: "RequiereCuidador",
                table: "censo");

            migrationBuilder.AddColumn<string>(
                name: "Programa",
                table: "censo",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SistemasPresionNegativaVac",
                table: "censo",
                type: "character varying(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClinicaHeridas",
                table: "censo",
                type: "character varying(2)",
                maxLength: 2,
                nullable: true);
        }
    }
}
