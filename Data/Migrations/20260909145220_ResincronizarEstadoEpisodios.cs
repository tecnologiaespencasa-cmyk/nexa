using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nexa.Data.Migrations
{
    /// <summary>
    /// Vuelve a poner el estado abierto/cerrado de cada episodio de acuerdo con el estado real de
    /// su atención.
    ///
    /// El problema: <c>censo_paciente_programa."CerradoAtUtc"</c> es una copia de un hecho que en
    /// realidad vive en el registro del censo. La copia se refresca en
    /// <c>CensoPacienteService.ReconciliarEpisodiosAsync</c>, que corre al construir la pantalla
    /// unificada, es decir, solo para el paciente que alguien abre. Los episodios los sembró
    /// BackfillCensoPaciente el 28-08 con el estado correcto de ese día; las altas que se dieron
    /// después en producción —donde el censo unificado todavía no está— no tuvieron quién
    /// actualizara la copia. Al 09-09 quedaron <b>245 pacientes</b> que el carril muestra como
    /// activos con su atención dada de alta: 241 en agudos, 4 en crónicos y 4 en terapia.
    ///
    /// Esta migración los pone al día de una sola vez, con las mismas reglas de <c>Conciliar</c>.
    /// Es idempotente: correrla otra vez no cambia nada. Después del despliegue la reconciliación
    /// por pantalla basta, porque los cinco programas guardan y redirigen a la vista unificada,
    /// que reconcilia al paciente antes de pintar el carril.
    ///
    /// Solo escribe las tres columnas de estado de <c>censo_paciente_programa</c>. No toca ningún
    /// dato clínico, no borra episodios y no altera el estado de ningún censo. Los episodios sin
    /// <c>RegistroId</c> —programa asignado que todavía nadie diligenció— se dejan intactos.
    ///
    /// Respaldo previo de la tabla completa (2909 filas):
    /// C:\tmp-nexa\respaldo-episodios\censo_paciente_programa.{json,csv}
    /// </summary>
    public partial class ResincronizarEstadoEpisodios : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(Resincronizar);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No hay vuelta atrás posible: el estado anterior era justamente el desactualizado, y
            // reconstruirlo exigiría saber qué episodio estaba mal y por qué. Si hiciera falta, se
            // carga el respaldo citado arriba.
        }

        // Las condiciones replican una por una las de CensoPacienteService:
        //   agudos   -> EsAgudoCerrado: el estado menciona alta, cancelado o rechazado.
        //   crónicos -> egreso real (CensoVisibility.HayEgreso) o estado "Inactivo".
        //   heridas  -> EsProgramaCerrado: egreso real, o un estado presente distinto de "Activo".
        //   NPT      -> igual que heridas.
        //   terapia  -> alta "Cerrado", o estado del paciente distinto de "Activo".
        //
        // "Egreso real" es >= 1900-01-01: una carga antigua dejó fechas 0001-01-01 que no son
        // egresos, y tomarlas por tales fue justo lo que sacó al paciente 3380199 del informe. Por
        // eso la regla de crónicos aquí ya no es la del backfill ("FechaEgreso IS NOT NULL").
        private const string Resincronizar = """
            -- ---------------------------------------------------------------- AGUDOS: cerrar
            UPDATE censo_paciente_programa e
            SET "CerradoAtUtc" = coalesce(x."FechaAlta"::timestamptz, now()),
                "CerradoPor"   = 'Resincronizacion',
                "MotivoCierre" = left(x."Estado", 120)
            FROM censo x
            WHERE e."Programa" = 'AGUDOS'
              AND e."RegistroId" = x."Id"
              AND e."CerradoAtUtc" IS NULL
              AND (x."FarmaciaProrrogaDeId" IS NULL
                   OR NOT EXISTS (SELECT 1 FROM censo pa WHERE pa."Id" = x."FarmaciaProrrogaDeId"))
              AND (x."FarmaciaProrrogaVersionId" IS NULL
                   OR NOT EXISTS (SELECT 1 FROM censo_prorrogas pr WHERE pr."Id" = x."FarmaciaProrrogaVersionId"))
              AND (coalesce(x."Estado", '') ILIKE '%alta%'
                   OR coalesce(x."Estado", '') ILIKE '%cancelado%'
                   OR coalesce(x."Estado", '') ILIKE '%rechazado%');

            -- AGUDOS: reabrir lo que volvió a estar activo
            UPDATE censo_paciente_programa e
            SET "CerradoAtUtc" = NULL, "CerradoPor" = NULL, "MotivoCierre" = NULL
            FROM censo x
            WHERE e."Programa" = 'AGUDOS'
              AND e."RegistroId" = x."Id"
              AND e."CerradoAtUtc" IS NOT NULL
              AND (x."FarmaciaProrrogaDeId" IS NULL
                   OR NOT EXISTS (SELECT 1 FROM censo pa WHERE pa."Id" = x."FarmaciaProrrogaDeId"))
              AND (x."FarmaciaProrrogaVersionId" IS NULL
                   OR NOT EXISTS (SELECT 1 FROM censo_prorrogas pr WHERE pr."Id" = x."FarmaciaProrrogaVersionId"))
              AND NOT (coalesce(x."Estado", '') ILIKE '%alta%'
                       OR coalesce(x."Estado", '') ILIKE '%cancelado%'
                       OR coalesce(x."Estado", '') ILIKE '%rechazado%');

            -- -------------------------------------------------------------- CRONICOS: cerrar
            UPDATE censo_paciente_programa e
            SET "CerradoAtUtc" = CASE WHEN x."FechaEgreso" >= DATE '1900-01-01'
                                      THEN x."FechaEgreso"::timestamptz ELSE now() END,
                "CerradoPor"   = 'Resincronizacion',
                "MotivoCierre" = left(coalesce(x."MotivoEgreso", x."EstadoPaciente"), 120)
            FROM censo_cronicos x
            WHERE e."Programa" = 'CRONICOS'
              AND e."RegistroId" = x."Id"
              AND e."CerradoAtUtc" IS NULL
              AND (x."FechaEgreso" >= DATE '1900-01-01'
                   OR coalesce(x."EstadoPaciente", '') ILIKE 'Inactivo');

            -- CRONICOS: reabrir
            UPDATE censo_paciente_programa e
            SET "CerradoAtUtc" = NULL, "CerradoPor" = NULL, "MotivoCierre" = NULL
            FROM censo_cronicos x
            WHERE e."Programa" = 'CRONICOS'
              AND e."RegistroId" = x."Id"
              AND e."CerradoAtUtc" IS NOT NULL
              AND NOT (x."FechaEgreso" >= DATE '1900-01-01')
              AND coalesce(x."EstadoPaciente", '') NOT ILIKE 'Inactivo';

            -- ------------------------------------------------------- CLINICA_HERIDAS: cerrar
            UPDATE censo_paciente_programa e
            SET "CerradoAtUtc" = CASE WHEN x."FechaEgreso" >= DATE '1900-01-01'
                                      THEN x."FechaEgreso"::timestamptz ELSE now() END,
                "CerradoPor"   = 'Resincronizacion',
                "MotivoCierre" = left(coalesce(x."MotivoEgreso", x."Estado"), 120)
            FROM censo_clinica_heridas x
            WHERE e."Programa" = 'CLINICA_HERIDAS'
              AND e."RegistroId" = x."Id"
              AND e."CerradoAtUtc" IS NULL
              AND (x."FechaEgreso" >= DATE '1900-01-01'
                   OR (btrim(coalesce(x."Estado", '')) <> '' AND x."Estado" NOT ILIKE 'Activo'));

            -- CLINICA_HERIDAS: reabrir
            UPDATE censo_paciente_programa e
            SET "CerradoAtUtc" = NULL, "CerradoPor" = NULL, "MotivoCierre" = NULL
            FROM censo_clinica_heridas x
            WHERE e."Programa" = 'CLINICA_HERIDAS'
              AND e."RegistroId" = x."Id"
              AND e."CerradoAtUtc" IS NOT NULL
              AND NOT (x."FechaEgreso" >= DATE '1900-01-01')
              AND (btrim(coalesce(x."Estado", '')) = '' OR x."Estado" ILIKE 'Activo');

            -- ------------------------------------------------------------------- NPT: cerrar
            UPDATE censo_paciente_programa e
            SET "CerradoAtUtc" = CASE WHEN x."FechaEgreso" >= DATE '1900-01-01'
                                      THEN x."FechaEgreso"::timestamptz ELSE now() END,
                "CerradoPor"   = 'Resincronizacion',
                "MotivoCierre" = left(coalesce(x."MotivoEgreso", x."Estado"), 120)
            FROM censo_npt x
            WHERE e."Programa" = 'NPT'
              AND e."RegistroId" = x."Id"
              AND e."CerradoAtUtc" IS NULL
              AND (x."FechaEgreso" >= DATE '1900-01-01'
                   OR (btrim(coalesce(x."Estado", '')) <> '' AND x."Estado" NOT ILIKE 'Activo'));

            -- NPT: reabrir
            UPDATE censo_paciente_programa e
            SET "CerradoAtUtc" = NULL, "CerradoPor" = NULL, "MotivoCierre" = NULL
            FROM censo_npt x
            WHERE e."Programa" = 'NPT'
              AND e."RegistroId" = x."Id"
              AND e."CerradoAtUtc" IS NOT NULL
              AND NOT (x."FechaEgreso" >= DATE '1900-01-01')
              AND (btrim(coalesce(x."Estado", '')) = '' OR x."Estado" ILIKE 'Activo');

            -- --------------------------------------------------- TERAPIA_AMBULATORIA: cerrar
            UPDATE censo_paciente_programa e
            SET "CerradoAtUtc" = coalesce(x."FechaAlta"::timestamptz, now()),
                "CerradoPor"   = 'Resincronizacion',
                "MotivoCierre" = left(coalesce(x."MotivoAlta", x."EstadoPaciente"), 120)
            FROM censo_terapias_ambulatorias x
            WHERE e."Programa" = 'TERAPIA_AMBULATORIA'
              AND e."RegistroId" = x."Id"
              AND e."CerradoAtUtc" IS NULL
              AND (coalesce(x."EstadoAlta", '') ILIKE 'Cerrado'
                   OR coalesce(x."EstadoPaciente", '') NOT ILIKE 'Activo');

            -- TERAPIA_AMBULATORIA: reabrir
            UPDATE censo_paciente_programa e
            SET "CerradoAtUtc" = NULL, "CerradoPor" = NULL, "MotivoCierre" = NULL
            FROM censo_terapias_ambulatorias x
            WHERE e."Programa" = 'TERAPIA_AMBULATORIA'
              AND e."RegistroId" = x."Id"
              AND e."CerradoAtUtc" IS NOT NULL
              AND coalesce(x."EstadoAlta", '') NOT ILIKE 'Cerrado'
              AND coalesce(x."EstadoPaciente", '') ILIKE 'Activo';
            """;
    }
}
