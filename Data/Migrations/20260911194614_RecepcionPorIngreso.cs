using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nexa.Data.Migrations
{
    /// <summary>
    /// La recepción pasa de ser del paciente a ser del ingreso.
    ///
    /// Vivía una sola vez en censo_paciente, así que un paciente que reingresaba heredaba la
    /// recepción del ingreso anterior y la pisaba al guardar la nueva. Cada ingreso nace de un
    /// correo distinto: la recepción es de la atención.
    ///
    /// Esta migración solo AGREGA las columnas al episodio y traslada lo que se puede recuperar.
    /// Las del maestro se retiran en una migración aparte, después de verificar este traslado
    /// contra la base: primero se comprueba que el dato llegó, y solo entonces se borra el origen.
    ///
    /// De dónde sale cada dato:
    ///  - AGUDOS: de su propia tabla, que sí guardaba los 7 campos por atención. Es el histórico
    ///    real y exacto; hay 106 documentos cuya recepción difiere de verdad entre atenciones. Se
    ///    leen solo las filas que un episodio referencia, para dejar fuera las copias de despacho
    ///    de farmacia, que son filas ocultas y no atenciones.
    ///  - Los otros cuatro censos no tienen ningún campo de recepción, así que la única que existe
    ///    es la del maestro y se asigna a la atención más reciente de cada programa —decisión del
    ///    usuario—, dejando las anteriores vacías. Solo se traslada cuando el maestro trae
    ///    recepción de verdad (quién recepcionó, quién hace kardex o fecha de respuesta): si lo
    ///    único que hay es la fecha, esa fecha ya es la de ingreso al programa y vive en la tabla
    ///    del censo, no es una recepción.
    ///
    /// Respaldo previo: C:\tmp-nexa\respaldo-recepcion\ (censo_paciente, censo_paciente_programa y
    /// la recepción por atención de agudos, al 2026-09-11).
    /// </summary>
    public partial class RecepcionPorIngreso : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "FechaIngreso",
                table: "censo_paciente_programa",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaRespuesta",
                table: "censo_paciente_programa",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<TimeSpan>(
                name: "HoraIngreso",
                table: "censo_paciente_programa",
                type: "interval",
                nullable: true);

            migrationBuilder.AddColumn<TimeSpan>(
                name: "HoraRespuesta",
                table: "censo_paciente_programa",
                type: "interval",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "IndicadorTiempoRespuestaMinutos",
                table: "censo_paciente_programa",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NombreRealizaKardex",
                table: "censo_paciente_programa",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NombreRecepcionaCaso",
                table: "censo_paciente_programa",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            // ----- Agudos: su histórico real, atención por atención -----
            // El nombre en blanco se guarda como NULL y no como cadena vacía, para que
            // "sin recepción registrada" sea un solo estado y no dos.
            migrationBuilder.Sql("""
                UPDATE censo_paciente_programa e
                SET "FechaIngreso" = c."FechaIngreso",
                    "HoraIngreso" = c."HoraIngreso",
                    "FechaRespuesta" = c."FechaRespuesta",
                    "HoraRespuesta" = c."HoraRespuesta",
                    "IndicadorTiempoRespuestaMinutos" = c."IndicadorTiempoRespuestaMinutos",
                    "NombreRecepcionaCaso" = NULLIF(btrim(c."NombreRecepcionaCaso"), ''),
                    "NombreRealizaKardex" = NULLIF(btrim(c."NombreRealizaKardex"), '')
                FROM censo c
                WHERE e."Programa" = 'AGUDOS'
                  AND e."RegistroId" = c."Id";
                """);

            // ----- Los otros cuatro: la única recepción del maestro, a la atención más reciente -----
            // "Más reciente" se resuelve con la fecha de la propia atención —la misma columna con
            // la que el selector de atenciones las ordena en pantalla— y no con el orden en que se
            // crearon los episodios, que en el histórico los sembró un backfill todos el mismo día.
            migrationBuilder.Sql("""
                WITH fecha_atencion AS (
                    SELECT e."Id",
                           e."CensoPacienteId",
                           e."Programa",
                           COALESCE(cr."FechaIngreso",
                                    ch."FechaIngresoPrograma",
                                    np."FechaIngresoPrograma",
                                    ta."FechaInicio",
                                    (e."AgregadoAtUtc" AT TIME ZONE 'UTC')::date) AS fecha
                    FROM censo_paciente_programa e
                    LEFT JOIN censo_cronicos cr
                           ON e."Programa" = 'CRONICOS' AND cr."Id" = e."RegistroId"
                    LEFT JOIN censo_clinica_heridas ch
                           ON e."Programa" = 'CLINICA_HERIDAS' AND ch."Id" = e."RegistroId"
                    LEFT JOIN censo_npt np
                           ON e."Programa" = 'NPT' AND np."Id" = e."RegistroId"
                    LEFT JOIN censo_terapias_ambulatorias ta
                           ON e."Programa" = 'TERAPIA_AMBULATORIA' AND ta."Id" = e."RegistroId"
                    WHERE e."Programa" <> 'AGUDOS'
                ),
                ultima AS (
                    SELECT DISTINCT ON ("CensoPacienteId", "Programa") "Id"
                    FROM fecha_atencion
                    ORDER BY "CensoPacienteId", "Programa", fecha DESC, "Id" DESC
                )
                UPDATE censo_paciente_programa e
                SET "FechaIngreso" = p."FechaIngreso",
                    "HoraIngreso" = NULLIF(p."HoraIngreso", INTERVAL '0'),
                    "FechaRespuesta" = p."FechaRespuesta",
                    "HoraRespuesta" = p."HoraRespuesta",
                    "IndicadorTiempoRespuestaMinutos" = p."IndicadorTiempoRespuestaMinutos",
                    "NombreRecepcionaCaso" = NULLIF(btrim(p."NombreRecepcionaCaso"), ''),
                    "NombreRealizaKardex" = NULLIF(btrim(p."NombreRealizaKardex"), '')
                FROM censo_paciente p
                WHERE e."Id" IN (SELECT "Id" FROM ultima)
                  AND p."Id" = e."CensoPacienteId"
                  AND (NULLIF(btrim(p."NombreRecepcionaCaso"), '') IS NOT NULL
                       OR NULLIF(btrim(p."NombreRealizaKardex"), '') IS NOT NULL
                       OR p."FechaRespuesta" IS NOT NULL);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FechaIngreso",
                table: "censo_paciente_programa");

            migrationBuilder.DropColumn(
                name: "FechaRespuesta",
                table: "censo_paciente_programa");

            migrationBuilder.DropColumn(
                name: "HoraIngreso",
                table: "censo_paciente_programa");

            migrationBuilder.DropColumn(
                name: "HoraRespuesta",
                table: "censo_paciente_programa");

            migrationBuilder.DropColumn(
                name: "IndicadorTiempoRespuestaMinutos",
                table: "censo_paciente_programa");

            migrationBuilder.DropColumn(
                name: "NombreRealizaKardex",
                table: "censo_paciente_programa");

            migrationBuilder.DropColumn(
                name: "NombreRecepcionaCaso",
                table: "censo_paciente_programa");
        }
    }
}
