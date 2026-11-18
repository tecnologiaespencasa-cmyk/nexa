using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nexa.Data.Migrations
{
    /// <inheritdoc />
    public partial class AgregarServicioAsistencia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ServicioAutorizado",
                table: "censo_cuidadores",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TipoAsistencia",
                table: "censo_cronicos",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            // Relleno acordado con el usuario el 2026-11-18. Solo escribe las columnas nuevas, que
            // nacen vacías: no cambia ningún dato que ya existiera.
            // - Hasta hoy el programa Cuidadores solo daba cuidador: todas sus atenciones quedan «Cuidador».
            // - En crónicos, «Requiere cuidador = Sí» pasa a «Requiere asistencia = Sí» con tipo «Cuidador».
            migrationBuilder.Sql("""
                UPDATE censo_cuidadores SET "ServicioAutorizado" = 'Cuidador' WHERE "ServicioAutorizado" IS NULL;
                UPDATE censo_cronicos SET "TipoAsistencia" = 'Cuidador'
                WHERE "TipoAsistencia" IS NULL AND upper(translate(coalesce("RequiereCuidador", ''), 'í', 'i')) = 'SI';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ServicioAutorizado",
                table: "censo_cuidadores");

            migrationBuilder.DropColumn(
                name: "TipoAsistencia",
                table: "censo_cronicos");
        }
    }
}
