using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nexa.Data.Migrations
{
    /// <summary>
    /// Índices de la tabla censo para abrir la ficha del paciente (2026-09-24). Solo agrega índices:
    /// no toca datos ni columnas. Medido con EXPLAIN ANALYZE en la base real, en un ensayo revertido:
    /// la conciliación de episodios pasaba de un recorrido completo (1.653 páginas, 25,8 ms en cada
    /// ficha) a 0,07 ms; las prórrogas aprobadas, de 5,1 a 0,05 ms, y la búsqueda por documento, de
    /// 2,3 a 0,08 ms. La tabla tiene menos de 5.000 filas, así que construirlos toma milisegundos.
    /// </summary>
    public partial class IndicesConsultaPacienteCenso : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_censo_FarmaciaProrrogaDeId",
                table: "censo",
                column: "FarmaciaProrrogaDeId");

            migrationBuilder.CreateIndex(
                name: "IX_censo_NumeroIdentificacion",
                table: "censo",
                column: "NumeroIdentificacion");

            // CensoPacienteService.ReconciliarEpisodiosAsync compara upper("NumeroIdentificacion"):
            // un índice sobre la columna no sirve para esa expresión. EF no modela índices de
            // expresión, así que va aquí y no en ApplicationDbContext.
            migrationBuilder.Sql(
                "CREATE INDEX IF NOT EXISTS \"IX_censo_NumeroIdentificacion_upper\" ON censo (upper(\"NumeroIdentificacion\"));");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_censo_NumeroIdentificacion_upper\";");

            migrationBuilder.DropIndex(
                name: "IX_censo_FarmaciaProrrogaDeId",
                table: "censo");

            migrationBuilder.DropIndex(
                name: "IX_censo_NumeroIdentificacion",
                table: "censo");
        }
    }
}
