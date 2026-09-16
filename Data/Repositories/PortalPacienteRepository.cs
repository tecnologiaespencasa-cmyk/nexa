using Nexa.Data.Repositories.Interfaces;
using Nexa.Data.Repositories.Models;
using Npgsql;

namespace Nexa.Data.Repositories;

/// <summary>
/// Lectura de las novedades y rondas intramurales de un paciente en el Portal Administrativo
/// (Neon). Solo consultas de lectura, igual que los demás repositorios de Neon.
/// </summary>
public class PortalPacienteRepository : IPortalPacienteRepository
{
    // Tope de filas por consulta. Un paciente real no se acerca, pero una hoja de vida no puede
    // quedar a merced de un documento mal digitado que coincida con cientos de reportes.
    private const int MaximoFilas = 300;

    private static readonly HashSet<string> ValoresDeRelleno =
        new(["No aplica", "N/A", "NA", "No", "Ninguno", "-"], StringComparer.OrdinalIgnoreCase);

    private readonly IConfiguration _configuration;

    public PortalPacienteRepository(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task<IReadOnlyList<PortalNovedadPacienteRow>> GetNovedadesPorDocumentoAsync(
        string documento,
        CancellationToken cancellationToken = default)
    {
        var clave = NormalizarDocumento(documento);
        if (clave.Length == 0)
        {
            return [];
        }

        await using var connection = new NpgsqlConnection(NeonConnectionString.FromConfiguration(_configuration));
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        // El documento lo escribe a mano quien reporta ("43.123.456", "CC 43123456"), así que se
        // compara solo por letras y dígitos. No hay índice sobre esa expresión en Neon: la tabla
        // se recorre completa. Si crece mucho, el portal puede agregar un índice de expresión con
        // esta misma fórmula sin que la intranet cambie.
        command.CommandText = """
            select
                n.id,
                coalesce(n."pacienteNombre", '') as paciente,
                n."createdAt",
                n."updatedAt",
                n.categoria::text as categoria,
                coalesce(
                    n."tipoPaciente"::text,
                    n."tipoFarmacia"::text,
                    n."tipoRuta"::text,
                    n."tipoTerapiaAmbulatoria"::text,
                    '') as tipo,
                n.estado::text as estado,
                coalesce(n.prioridad::text, '') as prioridad,
                coalesce(n.descripcion, '') as descripcion,
                coalesce(n."respuestaPrestador", '') as respuesta,
                coalesce(n."responsableGestion"::text, '') as responsable,
                coalesce(n."asignadoA", '') as asignado,
                coalesce(n."prestadorNombre", '') as prestador,
                coalesce(n."prestadorProfesion"::text, '') as profesion,
                n."esClinicaHeridas" as heridas,
                array_remove(array[n."medicamentoNombre1", n."medicamentoNombre2", n."medicamentoNombre3"], null) as medicamentos
            from public."Novedad" n
            where regexp_replace(upper(coalesce(n."pacienteDocumento", '')), '[^A-Z0-9]', '', 'g') = @documento
            order by n."createdAt" desc
            limit @limite;
            """;
        command.Parameters.AddWithValue("documento", clave);
        command.Parameters.AddWithValue("limite", MaximoFilas);

        var rows = new List<PortalNovedadPacienteRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new PortalNovedadPacienteRow
            {
                Id = GetString(reader, "id"),
                PacienteNombre = GetString(reader, "paciente").Trim(),
                CreatedAtUtc = GetUtc(reader, "createdAt"),
                UpdatedAtUtc = GetUtc(reader, "updatedAt"),
                Categoria = GetString(reader, "categoria"),
                Tipo = GetString(reader, "tipo"),
                Estado = GetString(reader, "estado"),
                Prioridad = GetString(reader, "prioridad"),
                Descripcion = GetString(reader, "descripcion"),
                RespuestaPrestador = GetString(reader, "respuesta"),
                ResponsableGestion = GetString(reader, "responsable"),
                AsignadoA = GetString(reader, "asignado"),
                PrestadorNombre = GetString(reader, "prestador"),
                PrestadorProfesion = GetString(reader, "profesion"),
                EsClinicaHeridas = !reader.IsDBNull(reader.GetOrdinal("heridas")) && reader.GetBoolean(reader.GetOrdinal("heridas")),
                Medicamentos = GetStringArray(reader, "medicamentos")
            });
        }

        return rows;
    }

    public async Task<IReadOnlyList<PortalRondaPacienteRow>> GetRondasPorDocumentoAsync(
        string documento,
        CancellationToken cancellationToken = default)
    {
        var clave = NormalizarDocumento(documento);
        if (clave.Length == 0)
        {
            return [];
        }

        await using var connection = new NpgsqlConnection(NeonConnectionString.FromConfiguration(_configuration));
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            select
                r.id,
                coalesce(r."pacienteNombre", '') as paciente,
                r."createdAt",
                r."fechaIngreso",
                coalesce(r.ips, '') as ips,
                coalesce(r."cie10Codigo", '') as cie10,
                coalesce(r."diagnosticoDescriptivo", '') as diagnostico,
                r."ingresoEfectivo",
                coalesce(r."causaNoIngreso", '') as causa,
                coalesce(r."observacionNoIngreso", '') as observacion,
                coalesce(r.otros, '') as otros,
                concat_ws(' ', u.nombres, u."primerApellido", u."segundoApellido") as reportado,
                coalesce(
                    (select array_agg(m.nombre order by m.orden)
                     from public."RondaMedicamento" m
                     where m."rondaId" = r.id),
                    array[]::text[]) as medicamentos
            from public."RondaIntramural" r
            left join public."User" u on u.id = r."usuarioId"
            where regexp_replace(upper(coalesce(r."pacienteDocumento", '')), '[^A-Z0-9]', '', 'g') = @documento
            order by r."createdAt" desc
            limit @limite;
            """;
        command.Parameters.AddWithValue("documento", clave);
        command.Parameters.AddWithValue("limite", MaximoFilas);

        var rows = new List<PortalRondaPacienteRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var ingresoOrdinal = reader.GetOrdinal("ingresoEfectivo");
            rows.Add(new PortalRondaPacienteRow
            {
                Id = GetString(reader, "id"),
                PacienteNombre = GetString(reader, "paciente").Trim(),
                CreatedAtUtc = GetUtc(reader, "createdAt"),
                FechaIngresoIps = reader.GetDateTime(reader.GetOrdinal("fechaIngreso")).Date,
                Ips = GetString(reader, "ips"),
                Cie10Codigo = GetString(reader, "cie10"),
                DiagnosticoDescriptivo = GetString(reader, "diagnostico"),
                IngresoEfectivo = reader.IsDBNull(ingresoOrdinal) ? null : reader.GetBoolean(ingresoOrdinal),
                CausaNoIngreso = GetString(reader, "causa"),
                ObservacionNoIngreso = GetString(reader, "observacion"),
                Otros = GetString(reader, "otros"),
                ReportadoPor = GetString(reader, "reportado").Trim(),
                Medicamentos = GetStringArray(reader, "medicamentos")
            });
        }

        return rows;
    }

    /// <summary>Solo letras y dígitos, en mayúsculas: la misma fórmula que aplica la consulta.</summary>
    private static string NormalizarDocumento(string? documento) =>
        new string((documento ?? string.Empty)
            .ToUpperInvariant()
            .Where(ch => ch is >= 'A' and <= 'Z' or >= '0' and <= '9')
            .ToArray());

    private static DateTime GetUtc(NpgsqlDataReader reader, string columnName) =>
        DateTime.SpecifyKind(reader.GetDateTime(reader.GetOrdinal(columnName)), DateTimeKind.Utc);

    private static string GetString(NpgsqlDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? string.Empty : Convert.ToString(reader.GetValue(ordinal)) ?? string.Empty;
    }

    private static IReadOnlyList<string> GetStringArray(NpgsqlDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        if (reader.IsDBNull(ordinal))
        {
            return [];
        }

        // El portal completa los medicamentos que no aplican con "No aplica": no son medicamentos.
        return reader.GetFieldValue<string[]>(ordinal)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Where(value => !ValoresDeRelleno.Contains(value))
            .ToList();
    }
}
