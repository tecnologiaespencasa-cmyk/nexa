using IntranetPrueba.Data.Repositories.Interfaces;
using IntranetPrueba.Data.Repositories.Models;
using Npgsql;
using NpgsqlTypes;

namespace IntranetPrueba.Data.Repositories;

public class PortalNovedadRepository : IPortalNovedadRepository
{
    private readonly IConfiguration _configuration;

    public PortalNovedadRepository(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task<IReadOnlyList<PortalNovedadRow>> GetNovedadesAsync(
        DateTime desde,
        DateTime hasta,
        string? categoria,
        string? auxiliar,
        CancellationToken cancellationToken = default)
    {
        var rows = new List<PortalNovedadRow>();
        await using var connection = new NpgsqlConnection(NeonConnectionString.FromConfiguration(_configuration));
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            select
                id,
                "createdAt",
                "updatedAt",
                categoria::text as categoria,
                estado::text as estado,
                coalesce(prioridad::text, '') as prioridad,
                coalesce("asignadoA", '') as asignadoA,
                coalesce("prestadorNombre", '') as prestadorNombre,
                coalesce("pacienteNombre", '') as pacienteNombre,
                coalesce("responsableGestion"::text, '') as responsableGestion
            from public."Novedad"
            where "createdAt" >= @desde
              and "createdAt" < @hasta
              and (@categoria is null or categoria::text = @categoria)
              and (
                    @auxiliar is null
                    or "asignadoA" = @auxiliar
                    or "prestadorNombre" = @auxiliar
                  )
            order by "createdAt" desc;
            """;
        command.Parameters.AddWithValue("desde", NpgsqlDbType.Timestamp, desde.Date);
        command.Parameters.AddWithValue("hasta", NpgsqlDbType.Timestamp, hasta.Date.AddDays(1));
        command.Parameters.AddWithValue("categoria", NpgsqlDbType.Text, string.IsNullOrWhiteSpace(categoria) ? DBNull.Value : categoria);
        command.Parameters.AddWithValue("auxiliar", NpgsqlDbType.Text, string.IsNullOrWhiteSpace(auxiliar) ? DBNull.Value : auxiliar);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new PortalNovedadRow
            {
                Id = GetString(reader, "id"),
                CreatedAt = reader.GetDateTime(reader.GetOrdinal("createdAt")),
                UpdatedAt = reader.GetDateTime(reader.GetOrdinal("updatedAt")),
                Categoria = GetString(reader, "categoria"),
                Estado = GetString(reader, "estado"),
                Prioridad = GetString(reader, "prioridad"),
                AsignadoA = GetString(reader, "asignadoA"),
                PrestadorNombre = GetString(reader, "prestadorNombre"),
                PacienteNombre = GetString(reader, "pacienteNombre"),
                ResponsableGestion = GetString(reader, "responsableGestion")
            });
        }

        return rows;
    }

    public async Task<IReadOnlyList<string>> GetCategoriasAsync(CancellationToken cancellationToken = default)
    {
        var categorias = new List<string>();
        await using var connection = new NpgsqlConnection(NeonConnectionString.FromConfiguration(_configuration));
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            select e.enumlabel
            from pg_type t
            join pg_enum e on t.oid = e.enumtypid
            join pg_namespace n on n.oid = t.typnamespace
            where n.nspname = 'public'
              and t.typname = 'CategoriaNovedad'
            order by e.enumsortorder;
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            categorias.Add(reader.GetString(0));
        }

        return categorias;
    }

    public async Task<IReadOnlyList<string>> GetAuxiliaresAsync(CancellationToken cancellationToken = default)
    {
        var auxiliares = new List<string>();
        await using var connection = new NpgsqlConnection(NeonConnectionString.FromConfiguration(_configuration));
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            select distinct value
            from (
                select nullif(trim("asignadoA"), '') as value from public."Novedad"
                union
                select nullif(trim("prestadorNombre"), '') as value from public."Novedad"
            ) source
            where value is not null
            order by value;
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            auxiliares.Add(reader.GetString(0));
        }

        return auxiliares;
    }

    private static string GetString(NpgsqlDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? string.Empty : Convert.ToString(reader.GetValue(ordinal)) ?? string.Empty;
    }
}
