using Nexa.Data.Repositories.Interfaces;
using Nexa.Data.Repositories.Models;
using Npgsql;
using NpgsqlTypes;

namespace Nexa.Data.Repositories;

public class PortalNovedadRepository : IPortalNovedadRepository
{
    private readonly IConfiguration _configuration;

    public PortalNovedadRepository(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task<IReadOnlyList<PortalNovedadRow>> GetNovedadesAsync(
        DateTime desdeUtc,
        DateTime hastaUtcExclusivo,
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
        // "createdAt" es timestamp sin zona con la hora UTC (así la escribe Prisma). Los límites llegan
        // ya en UTC y se comparan tal cual: sin truncarlos a fecha, que los correría cinco horas.
        command.Parameters.AddWithValue("desde", NpgsqlDbType.Timestamp, DateTime.SpecifyKind(desdeUtc, DateTimeKind.Unspecified));
        command.Parameters.AddWithValue("hasta", NpgsqlDbType.Timestamp, DateTime.SpecifyKind(hastaUtcExclusivo, DateTimeKind.Unspecified));
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

    public async Task<(int Pendientes, DateTime? MasAntiguaUtc)> GetPendientesAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(NeonConnectionString.FromConfiguration(_configuration));
        await connection.OpenAsync(cancellationToken);

        // Misma regla que el panel: pendiente es toda novedad que no está RESUELTA.
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select count(*)::int, min("createdAt")
            from public."Novedad"
            where estado::text <> 'RESUELTA';
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);
        return (reader.GetInt32(0), reader.IsDBNull(1) ? null : reader.GetDateTime(1));
    }

    public async Task<PortalNovedadesResumen> GetResumenDiaAsync(
        DateTime desdeUtc,
        DateTime hastaUtcExclusivo,
        DateTime ochoAmUtc,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(NeonConnectionString.FromConfiguration(_configuration));
        await connection.OpenAsync(cancellationToken);

        // Pendiente en un momento T: creada antes de T y, o sigue sin resolver, o se resolvió
        // (última actualización) después de T. Con T = ahora es exactamente lo que el panel
        // muestra como pendiente; con T en el pasado es la mejor reconstrucción posible.
        var areas = new List<PortalNovedadesArea>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                select
                    coalesce("responsableGestion"::text, '') as area,
                    count(*) filter (where "createdAt" < @hasta and (estado::text <> 'RESUELTA' or "updatedAt" >= @hasta))::int as pendientes,
                    count(*) filter (where "createdAt" >= @desde and "createdAt" < @hasta)::int as ingresadas,
                    count(*) filter (where estado::text = 'RESUELTA' and "updatedAt" >= @desde and "updatedAt" < @hasta)::int as resueltas,
                    count(*) filter (where "createdAt" < @ocho and (estado::text <> 'RESUELTA' or "updatedAt" >= @ocho))::int as saldo8,
                    min("createdAt") filter (where "createdAt" < @hasta and (estado::text <> 'RESUELTA' or "updatedAt" >= @hasta)) as mas_antigua
                from public."Novedad"
                group by 1;
                """;
            AgregarLimites(command, desdeUtc, hastaUtcExclusivo, ochoAmUtc);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                areas.Add(new PortalNovedadesArea
                {
                    Area = reader.GetString(0),
                    Pendientes = reader.GetInt32(1),
                    Ingresadas = reader.GetInt32(2),
                    Resueltas = reader.GetInt32(3),
                    Saldo8am = reader.GetInt32(4),
                    MasAntiguaPendienteUtc = reader.IsDBNull(5) ? null : reader.GetDateTime(5)
                });
            }
        }

        var resueltas = new List<DateTime>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                select "updatedAt"
                from public."Novedad"
                where estado::text = 'RESUELTA' and "updatedAt" >= @desde and "updatedAt" < @hasta
                order by "updatedAt";
                """;
            AgregarLimites(command, desdeUtc, hastaUtcExclusivo, ochoAmUtc);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                resueltas.Add(reader.GetDateTime(0));
            }
        }

        return new PortalNovedadesResumen { Areas = areas, ResueltasUtc = resueltas };
    }

    // "createdAt"/"updatedAt" son timestamp sin zona con la hora UTC: los límites van tal cual.
    private static void AgregarLimites(NpgsqlCommand command, DateTime desdeUtc, DateTime hastaUtc, DateTime ochoUtc)
    {
        command.Parameters.AddWithValue("desde", NpgsqlDbType.Timestamp, DateTime.SpecifyKind(desdeUtc, DateTimeKind.Unspecified));
        command.Parameters.AddWithValue("hasta", NpgsqlDbType.Timestamp, DateTime.SpecifyKind(hastaUtc, DateTimeKind.Unspecified));
        command.Parameters.AddWithValue("ocho", NpgsqlDbType.Timestamp, DateTime.SpecifyKind(ochoUtc, DateTimeKind.Unspecified));
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
