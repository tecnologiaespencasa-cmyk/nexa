using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nexa.Data.Migrations
{
    /// <summary>
    /// ⚠️ APLICAR SOLO DESPUÉS DE DESPLEGAR EL CÓDIGO DE ESTE CAMBIO.
    ///
    /// El binario anterior tiene estas siete columnas en su modelo de EF y las pide en cada
    /// consulta a censo_paciente, así que borrarlas antes del despliegue deja la pantalla del
    /// censo tirando "column does not exist". Se aplicó por error el 2026-09-11 sobre la base en
    /// uso y hubo que revertirla y repoblar el maestro desde el respaldo; queda pendiente a
    /// propósito. Primero se despliega, después se aplica.
    ///
    /// Retira del maestro las siete columnas de recepción, que ya viven en el episodio.
    ///
    /// RecepcionPorIngreso las agregó y trasladó los datos, y el traslado se verificó contra la
    /// base antes de borrar el origen: 2.885 episodios de agudos con su recepción real por
    /// atención, y 94 pacientes cuyos ingresos tienen ahora recepciones distintas entre sí, algo
    /// que con una sola fila por paciente no se podía ni representar.
    ///
    /// Dejarlas ahí sin uso habría creado el mismo problema que ya dio CerradoAtUtc: una copia que
    /// nadie refresca y que mañana alguien lee creyendo que es el dato vigente.
    ///
    /// De paso alinea el tipo de las dos horas del episodio. EF las creó como `interval` por
    /// omisión, pero son una hora del día y no un lapso; `censo` y el resto del censo ya usan
    /// `time without time zone` para lo mismo.
    ///
    /// Respaldo: C:\tmp-nexa\respaldo-recepcion\ (al 2026-09-11).
    /// </summary>
    public partial class QuitarRecepcionDelMaestro : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_censo_paciente_FechaIngreso",
                table: "censo_paciente");

            migrationBuilder.DropColumn(
                name: "FechaIngreso",
                table: "censo_paciente");

            migrationBuilder.DropColumn(
                name: "FechaRespuesta",
                table: "censo_paciente");

            migrationBuilder.DropColumn(
                name: "HoraIngreso",
                table: "censo_paciente");

            migrationBuilder.DropColumn(
                name: "HoraRespuesta",
                table: "censo_paciente");

            migrationBuilder.DropColumn(
                name: "IndicadorTiempoRespuestaMinutos",
                table: "censo_paciente");

            migrationBuilder.DropColumn(
                name: "NombreRealizaKardex",
                table: "censo_paciente");

            migrationBuilder.DropColumn(
                name: "NombreRecepcionaCaso",
                table: "censo_paciente");

            migrationBuilder.AlterColumn<TimeSpan>(
                name: "HoraRespuesta",
                table: "censo_paciente_programa",
                type: "time without time zone",
                nullable: true,
                oldClrType: typeof(TimeSpan),
                oldType: "interval",
                oldNullable: true);

            migrationBuilder.AlterColumn<TimeSpan>(
                name: "HoraIngreso",
                table: "censo_paciente_programa",
                type: "time without time zone",
                nullable: true,
                oldClrType: typeof(TimeSpan),
                oldType: "interval",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<TimeSpan>(
                name: "HoraRespuesta",
                table: "censo_paciente_programa",
                type: "interval",
                nullable: true,
                oldClrType: typeof(TimeSpan),
                oldType: "time without time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<TimeSpan>(
                name: "HoraIngreso",
                table: "censo_paciente_programa",
                type: "interval",
                nullable: true,
                oldClrType: typeof(TimeSpan),
                oldType: "time without time zone",
                oldNullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaIngreso",
                table: "censo_paciente",
                type: "date",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaRespuesta",
                table: "censo_paciente",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<TimeSpan>(
                name: "HoraIngreso",
                table: "censo_paciente",
                type: "time without time zone",
                nullable: false,
                defaultValue: new TimeSpan(0, 0, 0, 0, 0));

            migrationBuilder.AddColumn<TimeSpan>(
                name: "HoraRespuesta",
                table: "censo_paciente",
                type: "time without time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "IndicadorTiempoRespuestaMinutos",
                table: "censo_paciente",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NombreRealizaKardex",
                table: "censo_paciente",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NombreRecepcionaCaso",
                table: "censo_paciente",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_censo_paciente_FechaIngreso",
                table: "censo_paciente",
                column: "FechaIngreso");
        }
    }
}
