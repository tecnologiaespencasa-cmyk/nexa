using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nexa.Data.Migrations
{
    /// <summary>
    /// Backfill del maestro de paciente del censo.
    ///
    /// Crea un censo_paciente por documento a partir de las filas que ya existen en los cinco
    /// censos, las vincula y les abre su episodio de programa. Es idempotente: se puede volver a
    /// ejecutar sin duplicar nada.
    ///
    /// Garantia de continuidad operativa: no borra, no modifica y no reescribe ninguna columna
    /// existente de los censos. Lo unico que toca de las tablas de programa es la columna nueva
    /// CensoPacienteId, y solo cuando todavia esta vacia.
    /// </summary>
    public partial class BackfillCensoPaciente : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(CrearMaestros);
            migrationBuilder.Sql(VincularRegistros);
            migrationBuilder.Sql(CrearEpisodios);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No revierte nada a proposito. Deshacer este backfill significaria borrar los maestros y
            // los episodios, y la migracion anterior (AddCensoPacienteMaestro) ya elimina las dos
            // tablas y la columna de vinculo al revertirse. Hacerlo aqui tambien solo agregaria un
            // borrado innecesario sobre datos de produccion.
        }

        // --------------------------------------------------------------------------------------
        // 1. Un maestro por documento.
        //
        // Regla de ganador ante datos divergentes (en produccion hay 52 documentos con nombre
        // distinto y 92 con direccion distinta entre censos): gana el valor no vacio mas reciente.
        // "Mas reciente" ordena primero las filas reales sobre las copias internas de despacho a
        // farmacia, luego por fecha de ingreso, fecha de creacion e id. La eleccion es campo por
        // campo: si el registro ganador tiene un campo vacio, se completa con el mas reciente que si
        // lo tenga, de forma que la unificacion no pierda informacion.
        // --------------------------------------------------------------------------------------
        private const string CrearMaestros = """
            WITH fuente AS (
                SELECT
                    upper(btrim(c."NumeroIdentificacion"))              AS doc,
                    c."NumeroIdentificacion"::text                      AS numero,
                    CASE WHEN c."FarmaciaProrrogaDeId" IS NOT NULL
                           OR c."FarmaciaProrrogaVersionId" IS NOT NULL
                         THEN 1 ELSE 0 END                              AS es_copia,
                    c."FechaIngreso"                                    AS fecha_orden,
                    c."CreatedAtUtc"                                    AS creado_orden,
                    c."Id"                                              AS id_orden,
                    c."FechaIngreso"                                    AS fecha_ingreso,
                    c."HoraIngreso"                                     AS hora_ingreso,
                    c."FechaRespuesta"                                  AS fecha_respuesta,
                    c."HoraRespuesta"                                   AS hora_respuesta,
                    c."IndicadorTiempoRespuestaMinutos"                 AS indicador,
                    c."NombreRecepcionaCaso"::text                      AS recepciona,
                    c."NombreRealizaKardex"::text                       AS realiza_kardex,
                    c."TipoIdentificacion"::text                        AS tipo_doc,
                    c."NombrePaciente"::text                            AS nombre,
                    c."FechaNacimiento"                                 AS nacimiento,
                    c."Edad"                                            AS edad,
                    NULL::text                                          AS genero,
                    c."CorreoElectronico"::text                         AS correo,
                    c."CodigoCie10"::text                               AS cie10,
                    c."DiagnosticoDescriptivo"::text                    AS diagnostico,
                    c."Asegurador"::text                                AS asegurador,
                    c."Direccion"::text                                 AS direccion,
                    c."DireccionValidada"                               AS dir_validada,
                    c."AsumirDireccionErrada"                           AS dir_asumir,
                    c."DetalleDireccion"::text                          AS detalle_dir,
                    c."ClasificacionZonaSura"::text                     AS zona_sura,
                    c."MunicipioResidencia"::text                       AS municipio,
                    c."Barrio"::text                                    AS barrio,
                    c."ZonaDireccionSegunMunicipio"::text               AS zona_dir,
                    c."Area"::text                                      AS area,
                    c."IpsQueRemite"::text                              AS ips,
                    c."VistoBuenoRangoFueraAnexo"::text                 AS visto_bueno,
                    c."Telefono1"::text                                 AS tel1,
                    c."Telefono2"::text                                 AS tel2,
                    c."Telefono3"::text                                 AS tel3
                FROM censo c
                WHERE nullif(btrim(c."NumeroIdentificacion"), '') IS NOT NULL

                UNION ALL

                SELECT
                    upper(btrim(k."NumeroIdentificacion")), k."NumeroIdentificacion"::text, 0,
                    k."FechaIngreso", k."CreatedAtUtc", k."Id",
                    k."FechaIngreso", NULL::time, NULL::date, NULL::time, NULL::integer,
                    NULL::text, NULL::text,
                    k."TipoIdentificacion"::text, k."NombrePaciente"::text, k."FechaNacimiento", k."Edad",
                    k."Genero"::text, k."CorreoElectronico"::text,
                    NULL::text, NULL::text, NULL::text,
                    k."Direccion"::text, k."DireccionValidada", k."AsumirDireccionErrada", k."DetalleDireccion"::text,
                    k."ClasificacionZonaSura"::text, k."MunicipioResidencia"::text, k."Barrio"::text,
                    k."ZonaDireccionSegunMunicipio"::text, k."Area"::text,
                    NULL::text, NULL::text,
                    NULL::text, NULL::text, NULL::text
                FROM censo_cronicos k
                WHERE nullif(btrim(k."NumeroIdentificacion"), '') IS NOT NULL

                UNION ALL

                SELECT
                    upper(btrim(h."NumeroIdentificacion")), h."NumeroIdentificacion"::text, 0,
                    h."FechaIngresoPrograma", h."CreatedAtUtc", h."Id",
                    h."FechaIngresoPrograma", NULL::time, NULL::date, NULL::time, NULL::integer,
                    NULL::text, NULL::text,
                    h."TipoIdentificacion"::text, h."NombrePaciente"::text, h."FechaNacimiento", h."Edad",
                    h."Genero"::text, NULL::text,
                    h."CodigoCie10"::text, h."DiagnosticoDescriptivo"::text, h."Asegurador"::text,
                    h."Direccion"::text, h."DireccionValidada", h."AsumirDireccionErrada", h."DetalleDireccion"::text,
                    h."ClasificacionZonaSura"::text, h."MunicipioResidencia"::text, h."Barrio"::text,
                    h."ZonaDireccionSegunMunicipio"::text, NULL::text,
                    NULL::text, NULL::text,
                    h."TelefonoPrincipal"::text, h."TelefonoAdicional1"::text, h."TelefonoAdicional2"::text
                FROM censo_clinica_heridas h
                WHERE nullif(btrim(h."NumeroIdentificacion"), '') IS NOT NULL

                UNION ALL

                SELECT
                    upper(btrim(n."NumeroIdentificacion")), n."NumeroIdentificacion"::text, 0,
                    n."FechaIngresoPrograma", n."CreatedAtUtc", n."Id",
                    n."FechaIngresoPrograma", NULL::time, NULL::date, NULL::time, NULL::integer,
                    NULL::text, NULL::text,
                    n."TipoIdentificacion"::text, n."NombrePaciente"::text, n."FechaNacimiento", n."Edad",
                    n."Genero"::text, NULL::text,
                    n."CodigoCie10"::text, n."DiagnosticoDescriptivo"::text, n."Asegurador"::text,
                    n."Direccion"::text, n."DireccionValidada", n."AsumirDireccionErrada", NULL::text,
                    n."ClasificacionZonaSura"::text, n."MunicipioResidencia"::text, n."Barrio"::text,
                    n."ZonaDireccionSegunMunicipio"::text, NULL::text,
                    NULL::text, NULL::text,
                    n."TelefonoPrincipal"::text, n."TelefonoAdicional1"::text, n."TelefonoAdicional2"::text
                FROM censo_npt n
                WHERE nullif(btrim(n."NumeroIdentificacion"), '') IS NOT NULL

                UNION ALL

                SELECT
                    upper(btrim(t."NumeroIdentificacion")), t."NumeroIdentificacion"::text, 0,
                    t."FechaIngreso", t."CreatedAtUtc", t."Id",
                    t."FechaIngreso", NULL::time, NULL::date, NULL::time, NULL::integer,
                    NULL::text, NULL::text,
                    t."TipoIdentificacion"::text, t."NombrePaciente"::text, t."FechaNacimiento", t."Edad",
                    NULL::text, t."CorreoElectronico"::text,
                    t."CodigoCie10"::text, t."DiagnosticoDescriptivo"::text, NULL::text,
                    t."Direccion"::text, t."DireccionValidada", t."AsumirDireccionErrada", t."DetalleDireccion"::text,
                    t."ClasificacionZonaSura"::text, t."MunicipioResidencia"::text, t."Barrio"::text,
                    t."ZonaDireccionSegunMunicipio"::text, t."Area"::text,
                    t."IpsQueRemite"::text, NULL::text,
                    t."TelefonoPrincipal"::text, t."TelefonoAdicional1"::text, t."TelefonoAdicional2"::text
                FROM censo_terapias_ambulatorias t
                WHERE nullif(btrim(t."NumeroIdentificacion"), '') IS NOT NULL
            ),
            consolidado AS (
                SELECT
                    doc,
                    (array_agg(numero          ORDER BY es_copia, fecha_orden DESC NULLS LAST, creado_orden DESC NULLS LAST, id_orden DESC) FILTER (WHERE nullif(btrim(numero), '')         IS NOT NULL))[1] AS numero,
                    (array_agg(tipo_doc        ORDER BY es_copia, fecha_orden DESC NULLS LAST, creado_orden DESC NULLS LAST, id_orden DESC) FILTER (WHERE nullif(btrim(tipo_doc), '')       IS NOT NULL))[1] AS tipo_doc,
                    (array_agg(nombre          ORDER BY es_copia, fecha_orden DESC NULLS LAST, creado_orden DESC NULLS LAST, id_orden DESC) FILTER (WHERE nullif(btrim(nombre), '')         IS NOT NULL))[1] AS nombre,
                    (array_agg(nacimiento      ORDER BY es_copia, fecha_orden DESC NULLS LAST, creado_orden DESC NULLS LAST, id_orden DESC) FILTER (WHERE nacimiento     IS NOT NULL))[1] AS nacimiento,
                    (array_agg(edad            ORDER BY es_copia, fecha_orden DESC NULLS LAST, creado_orden DESC NULLS LAST, id_orden DESC) FILTER (WHERE edad           IS NOT NULL))[1] AS edad,
                    (array_agg(genero          ORDER BY es_copia, fecha_orden DESC NULLS LAST, creado_orden DESC NULLS LAST, id_orden DESC) FILTER (WHERE nullif(btrim(genero), '')         IS NOT NULL))[1] AS genero,
                    (array_agg(correo          ORDER BY es_copia, fecha_orden DESC NULLS LAST, creado_orden DESC NULLS LAST, id_orden DESC) FILTER (WHERE nullif(btrim(correo), '')         IS NOT NULL))[1] AS correo,
                    (array_agg(cie10           ORDER BY es_copia, fecha_orden DESC NULLS LAST, creado_orden DESC NULLS LAST, id_orden DESC) FILTER (WHERE nullif(btrim(cie10), '')          IS NOT NULL))[1] AS cie10,
                    (array_agg(diagnostico     ORDER BY es_copia, fecha_orden DESC NULLS LAST, creado_orden DESC NULLS LAST, id_orden DESC) FILTER (WHERE nullif(btrim(diagnostico), '')    IS NOT NULL))[1] AS diagnostico,
                    (array_agg(asegurador      ORDER BY es_copia, fecha_orden DESC NULLS LAST, creado_orden DESC NULLS LAST, id_orden DESC) FILTER (WHERE nullif(btrim(asegurador), '')     IS NOT NULL))[1] AS asegurador,
                    (array_agg(fecha_ingreso   ORDER BY es_copia, fecha_orden DESC NULLS LAST, creado_orden DESC NULLS LAST, id_orden DESC) FILTER (WHERE fecha_ingreso  IS NOT NULL))[1] AS fecha_ingreso,
                    (array_agg(hora_ingreso    ORDER BY es_copia, fecha_orden DESC NULLS LAST, creado_orden DESC NULLS LAST, id_orden DESC) FILTER (WHERE hora_ingreso   IS NOT NULL))[1] AS hora_ingreso,
                    (array_agg(fecha_respuesta ORDER BY es_copia, fecha_orden DESC NULLS LAST, creado_orden DESC NULLS LAST, id_orden DESC) FILTER (WHERE fecha_respuesta IS NOT NULL))[1] AS fecha_respuesta,
                    (array_agg(hora_respuesta  ORDER BY es_copia, fecha_orden DESC NULLS LAST, creado_orden DESC NULLS LAST, id_orden DESC) FILTER (WHERE hora_respuesta IS NOT NULL))[1] AS hora_respuesta,
                    (array_agg(indicador       ORDER BY es_copia, fecha_orden DESC NULLS LAST, creado_orden DESC NULLS LAST, id_orden DESC) FILTER (WHERE indicador      IS NOT NULL))[1] AS indicador,
                    (array_agg(recepciona      ORDER BY es_copia, fecha_orden DESC NULLS LAST, creado_orden DESC NULLS LAST, id_orden DESC) FILTER (WHERE nullif(btrim(recepciona), '')     IS NOT NULL))[1] AS recepciona,
                    (array_agg(realiza_kardex  ORDER BY es_copia, fecha_orden DESC NULLS LAST, creado_orden DESC NULLS LAST, id_orden DESC) FILTER (WHERE nullif(btrim(realiza_kardex), '') IS NOT NULL))[1] AS realiza_kardex,
                    -- La direccion y sus dos banderas salen juntas del mismo registro: el primero con
                    -- direccion no vacia. Elegirlas por separado podria marcar como validada una
                    -- direccion que en realidad vino de otra fila.
                    (array_agg(direccion       ORDER BY es_copia, fecha_orden DESC NULLS LAST, creado_orden DESC NULLS LAST, id_orden DESC) FILTER (WHERE nullif(btrim(direccion), '') IS NOT NULL))[1] AS direccion,
                    (array_agg(dir_validada    ORDER BY es_copia, fecha_orden DESC NULLS LAST, creado_orden DESC NULLS LAST, id_orden DESC) FILTER (WHERE nullif(btrim(direccion), '') IS NOT NULL))[1] AS dir_validada,
                    (array_agg(dir_asumir      ORDER BY es_copia, fecha_orden DESC NULLS LAST, creado_orden DESC NULLS LAST, id_orden DESC) FILTER (WHERE nullif(btrim(direccion), '') IS NOT NULL))[1] AS dir_asumir,
                    (array_agg(detalle_dir     ORDER BY es_copia, fecha_orden DESC NULLS LAST, creado_orden DESC NULLS LAST, id_orden DESC) FILTER (WHERE nullif(btrim(detalle_dir), '')    IS NOT NULL))[1] AS detalle_dir,
                    (array_agg(zona_sura       ORDER BY es_copia, fecha_orden DESC NULLS LAST, creado_orden DESC NULLS LAST, id_orden DESC) FILTER (WHERE nullif(btrim(zona_sura), '')      IS NOT NULL))[1] AS zona_sura,
                    (array_agg(municipio       ORDER BY es_copia, fecha_orden DESC NULLS LAST, creado_orden DESC NULLS LAST, id_orden DESC) FILTER (WHERE nullif(btrim(municipio), '')      IS NOT NULL))[1] AS municipio,
                    (array_agg(barrio          ORDER BY es_copia, fecha_orden DESC NULLS LAST, creado_orden DESC NULLS LAST, id_orden DESC) FILTER (WHERE nullif(btrim(barrio), '')         IS NOT NULL))[1] AS barrio,
                    (array_agg(zona_dir        ORDER BY es_copia, fecha_orden DESC NULLS LAST, creado_orden DESC NULLS LAST, id_orden DESC) FILTER (WHERE nullif(btrim(zona_dir), '')       IS NOT NULL))[1] AS zona_dir,
                    (array_agg(area            ORDER BY es_copia, fecha_orden DESC NULLS LAST, creado_orden DESC NULLS LAST, id_orden DESC) FILTER (WHERE nullif(btrim(area), '')           IS NOT NULL))[1] AS area,
                    (array_agg(ips             ORDER BY es_copia, fecha_orden DESC NULLS LAST, creado_orden DESC NULLS LAST, id_orden DESC) FILTER (WHERE nullif(btrim(ips), '')            IS NOT NULL))[1] AS ips,
                    (array_agg(visto_bueno     ORDER BY es_copia, fecha_orden DESC NULLS LAST, creado_orden DESC NULLS LAST, id_orden DESC) FILTER (WHERE nullif(btrim(visto_bueno), '')    IS NOT NULL))[1] AS visto_bueno,
                    (array_agg(tel1            ORDER BY es_copia, fecha_orden DESC NULLS LAST, creado_orden DESC NULLS LAST, id_orden DESC) FILTER (WHERE nullif(btrim(tel1), '')           IS NOT NULL))[1] AS tel1,
                    (array_agg(tel2            ORDER BY es_copia, fecha_orden DESC NULLS LAST, creado_orden DESC NULLS LAST, id_orden DESC) FILTER (WHERE nullif(btrim(tel2), '')           IS NOT NULL))[1] AS tel2,
                    (array_agg(tel3            ORDER BY es_copia, fecha_orden DESC NULLS LAST, creado_orden DESC NULLS LAST, id_orden DESC) FILTER (WHERE nullif(btrim(tel3), '')           IS NOT NULL))[1] AS tel3
                FROM fuente
                GROUP BY doc
            )
            INSERT INTO censo_paciente (
                "FechaIngreso", "HoraIngreso", "FechaRespuesta", "HoraRespuesta", "IndicadorTiempoRespuestaMinutos",
                "NombreRecepcionaCaso", "NombreRealizaKardex",
                "TipoIdentificacion", "NumeroIdentificacion", "NombrePaciente", "FechaNacimiento", "Edad",
                "Genero", "CorreoElectronico", "CodigoCie10", "DiagnosticoDescriptivo", "Asegurador",
                "Direccion", "DireccionValidada", "AsumirDireccionErrada", "DetalleDireccion",
                "ClasificacionZonaSura", "MunicipioResidencia", "Barrio", "ZonaDireccionSegunMunicipio", "Area",
                "IpsQueRemite", "VistoBuenoRangoFueraAnexo", "Telefono1", "Telefono2", "Telefono3",
                "CreatedAtUtc", "CreadoPor")
            SELECT
                coalesce(c.fecha_ingreso, CURRENT_DATE),
                coalesce(c.hora_ingreso, TIME '00:00:00'),
                c.fecha_respuesta, c.hora_respuesta, c.indicador,
                left(c.recepciona, 120), left(c.realiza_kardex, 120),
                left(coalesce(c.tipo_doc, ''), 3), left(coalesce(c.numero, c.doc), 20), left(coalesce(c.nombre, ''), 200),
                coalesce(c.nacimiento, DATE '1900-01-01'), coalesce(c.edad, 0),
                left(c.genero, 20), left(c.correo, 150), left(c.cie10, 4), left(c.diagnostico, 300), left(c.asegurador, 120),
                left(c.direccion, 300), coalesce(c.dir_validada, false), coalesce(c.dir_asumir, false), left(c.detalle_dir, 200),
                left(c.zona_sura, 30), left(c.municipio, 120), left(c.barrio, 120), left(c.zona_dir, 50), left(c.area, 10),
                left(c.ips, 200), left(c.visto_bueno, 2), left(c.tel1, 10), left(c.tel2, 10), left(c.tel3, 10),
                now(), 'Migracion inicial'
            FROM consolidado c
            WHERE NOT EXISTS (
                SELECT 1 FROM censo_paciente p
                WHERE upper(btrim(p."NumeroIdentificacion")) = c.doc);
            """;

        // --------------------------------------------------------------------------------------
        // 2. Vincular cada fila de programa con su maestro. Solo escribe la columna nueva y solo
        //    cuando esta vacia, de modo que una segunda ejecucion no pisa nada.
        // --------------------------------------------------------------------------------------
        private const string VincularRegistros = """
            UPDATE censo x SET "CensoPacienteId" = p."Id"
            FROM censo_paciente p
            WHERE x."CensoPacienteId" IS NULL
              AND upper(btrim(x."NumeroIdentificacion")) = upper(btrim(p."NumeroIdentificacion"));

            UPDATE censo_cronicos x SET "CensoPacienteId" = p."Id"
            FROM censo_paciente p
            WHERE x."CensoPacienteId" IS NULL
              AND upper(btrim(x."NumeroIdentificacion")) = upper(btrim(p."NumeroIdentificacion"));

            UPDATE censo_clinica_heridas x SET "CensoPacienteId" = p."Id"
            FROM censo_paciente p
            WHERE x."CensoPacienteId" IS NULL
              AND upper(btrim(x."NumeroIdentificacion")) = upper(btrim(p."NumeroIdentificacion"));

            UPDATE censo_npt x SET "CensoPacienteId" = p."Id"
            FROM censo_paciente p
            WHERE x."CensoPacienteId" IS NULL
              AND upper(btrim(x."NumeroIdentificacion")) = upper(btrim(p."NumeroIdentificacion"));

            UPDATE censo_terapias_ambulatorias x SET "CensoPacienteId" = p."Id"
            FROM censo_paciente p
            WHERE x."CensoPacienteId" IS NULL
              AND upper(btrim(x."NumeroIdentificacion")) = upper(btrim(p."NumeroIdentificacion"));
            """;

        // --------------------------------------------------------------------------------------
        // 3. Un episodio de programa por fila.
        //
        // Agudos excluye las copias internas de despacho a farmacia con el MISMO criterio que usa
        // la pantalla (Data/CensoVisibility.cs): una copia solo se ignora si su registro padre
        // todavia existe; si quedo huerfana si es una atencion visible y debe tener episodio.
        //
        // El cierre replica las reglas que ya usa cada censo: en agudos un estado de alta,
        // cancelacion o rechazo cierra la atencion; en los demas, el egreso o un estado distinto de
        // activo.
        // --------------------------------------------------------------------------------------
        private const string CrearEpisodios = """
            INSERT INTO censo_paciente_programa
                ("CensoPacienteId", "Programa", "RegistroId", "AgregadoAtUtc", "AgregadoPor", "CerradoAtUtc", "CerradoPor", "MotivoCierre")
            SELECT
                x."CensoPacienteId", 'AGUDOS', x."Id",
                coalesce(x."CreatedAtUtc", now()), 'Migracion inicial',
                CASE WHEN x."Estado" ILIKE '%alta%' OR x."Estado" ILIKE '%cancelado%' OR x."Estado" ILIKE '%rechazado%'
                     THEN coalesce(x."FechaAlta"::timestamptz, x."CreatedAtUtc", now()) END,
                CASE WHEN x."Estado" ILIKE '%alta%' OR x."Estado" ILIKE '%cancelado%' OR x."Estado" ILIKE '%rechazado%'
                     THEN 'Migracion inicial' END,
                CASE WHEN x."Estado" ILIKE '%alta%' OR x."Estado" ILIKE '%cancelado%' OR x."Estado" ILIKE '%rechazado%'
                     THEN left(x."Estado", 120) END
            FROM censo x
            WHERE x."CensoPacienteId" IS NOT NULL
              AND (x."FarmaciaProrrogaDeId" IS NULL
                   OR NOT EXISTS (SELECT 1 FROM censo pa WHERE pa."Id" = x."FarmaciaProrrogaDeId"))
              AND (x."FarmaciaProrrogaVersionId" IS NULL
                   OR NOT EXISTS (SELECT 1 FROM censo_prorrogas pr WHERE pr."Id" = x."FarmaciaProrrogaVersionId"))
              AND NOT EXISTS (SELECT 1 FROM censo_paciente_programa e
                              WHERE e."Programa" = 'AGUDOS' AND e."RegistroId" = x."Id");

            INSERT INTO censo_paciente_programa
                ("CensoPacienteId", "Programa", "RegistroId", "AgregadoAtUtc", "AgregadoPor", "CerradoAtUtc", "CerradoPor", "MotivoCierre")
            SELECT
                x."CensoPacienteId", 'CRONICOS', x."Id",
                coalesce(x."CreatedAtUtc", now()), 'Migracion inicial',
                CASE WHEN x."EstadoPaciente" ILIKE 'Inactivo' OR x."FechaEgreso" IS NOT NULL
                     THEN coalesce(x."FechaEgreso"::timestamptz, x."UpdatedAtUtc", x."CreatedAtUtc", now()) END,
                CASE WHEN x."EstadoPaciente" ILIKE 'Inactivo' OR x."FechaEgreso" IS NOT NULL
                     THEN 'Migracion inicial' END,
                CASE WHEN x."EstadoPaciente" ILIKE 'Inactivo' OR x."FechaEgreso" IS NOT NULL
                     THEN left(coalesce(x."MotivoEgreso", x."EstadoPaciente"), 120) END
            FROM censo_cronicos x
            WHERE x."CensoPacienteId" IS NOT NULL
              AND NOT EXISTS (SELECT 1 FROM censo_paciente_programa e
                              WHERE e."Programa" = 'CRONICOS' AND e."RegistroId" = x."Id");

            INSERT INTO censo_paciente_programa
                ("CensoPacienteId", "Programa", "RegistroId", "AgregadoAtUtc", "AgregadoPor", "CerradoAtUtc", "CerradoPor", "MotivoCierre")
            SELECT
                x."CensoPacienteId", 'CLINICA_HERIDAS', x."Id",
                coalesce(x."CreatedAtUtc", now()), 'Migracion inicial',
                CASE WHEN x."FechaEgreso" IS NOT NULL OR (x."Estado" IS NOT NULL AND x."Estado" NOT ILIKE 'Activo')
                     THEN coalesce(x."FechaEgreso"::timestamptz, x."UpdatedAtUtc", x."CreatedAtUtc", now()) END,
                CASE WHEN x."FechaEgreso" IS NOT NULL OR (x."Estado" IS NOT NULL AND x."Estado" NOT ILIKE 'Activo')
                     THEN 'Migracion inicial' END,
                CASE WHEN x."FechaEgreso" IS NOT NULL OR (x."Estado" IS NOT NULL AND x."Estado" NOT ILIKE 'Activo')
                     THEN left(coalesce(x."MotivoEgreso", x."Estado"), 120) END
            FROM censo_clinica_heridas x
            WHERE x."CensoPacienteId" IS NOT NULL
              AND NOT EXISTS (SELECT 1 FROM censo_paciente_programa e
                              WHERE e."Programa" = 'CLINICA_HERIDAS' AND e."RegistroId" = x."Id");

            INSERT INTO censo_paciente_programa
                ("CensoPacienteId", "Programa", "RegistroId", "AgregadoAtUtc", "AgregadoPor", "CerradoAtUtc", "CerradoPor", "MotivoCierre")
            SELECT
                x."CensoPacienteId", 'NPT', x."Id",
                coalesce(x."CreatedAtUtc", now()), 'Migracion inicial',
                CASE WHEN x."FechaEgreso" IS NOT NULL OR (x."Estado" IS NOT NULL AND x."Estado" NOT ILIKE 'Activo')
                     THEN coalesce(x."FechaEgreso"::timestamptz, x."UpdatedAtUtc", x."CreatedAtUtc", now()) END,
                CASE WHEN x."FechaEgreso" IS NOT NULL OR (x."Estado" IS NOT NULL AND x."Estado" NOT ILIKE 'Activo')
                     THEN 'Migracion inicial' END,
                CASE WHEN x."FechaEgreso" IS NOT NULL OR (x."Estado" IS NOT NULL AND x."Estado" NOT ILIKE 'Activo')
                     THEN left(coalesce(x."MotivoEgreso", x."Estado"), 120) END
            FROM censo_npt x
            WHERE x."CensoPacienteId" IS NOT NULL
              AND NOT EXISTS (SELECT 1 FROM censo_paciente_programa e
                              WHERE e."Programa" = 'NPT' AND e."RegistroId" = x."Id");

            INSERT INTO censo_paciente_programa
                ("CensoPacienteId", "Programa", "RegistroId", "AgregadoAtUtc", "AgregadoPor", "CerradoAtUtc", "CerradoPor", "MotivoCierre")
            SELECT
                x."CensoPacienteId", 'TERAPIA_AMBULATORIA', x."Id",
                coalesce(x."CreatedAtUtc", now()), 'Migracion inicial',
                CASE WHEN x."EstadoAlta" ILIKE 'Cerrado' OR x."EstadoPaciente" NOT ILIKE 'Activo'
                     THEN coalesce(x."FechaAlta"::timestamptz, x."UpdatedAtUtc", x."CreatedAtUtc", now()) END,
                CASE WHEN x."EstadoAlta" ILIKE 'Cerrado' OR x."EstadoPaciente" NOT ILIKE 'Activo'
                     THEN 'Migracion inicial' END,
                CASE WHEN x."EstadoAlta" ILIKE 'Cerrado' OR x."EstadoPaciente" NOT ILIKE 'Activo'
                     THEN left(coalesce(x."MotivoAlta", x."EstadoPaciente"), 120) END
            FROM censo_terapias_ambulatorias x
            WHERE x."CensoPacienteId" IS NOT NULL
              AND NOT EXISTS (SELECT 1 FROM censo_paciente_programa e
                              WHERE e."Programa" = 'TERAPIA_AMBULATORIA' AND e."RegistroId" = x."Id");
            """;
    }
}
