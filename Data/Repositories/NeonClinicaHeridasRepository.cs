using Nexa.Data.Repositories.Interfaces;
using Nexa.Data.Repositories.Models;
using Npgsql;

namespace Nexa.Data.Repositories;

/// <summary>
/// Lectura de los seguimientos de clínica de heridas que captura la aplicación del Portal
/// Administrativo en Neon. La intranet nunca escribe en esa base.
/// </summary>
public class NeonClinicaHeridasRepository : INeonClinicaHeridasRepository
{
    private readonly IConfiguration _configuration;

    public NeonClinicaHeridasRepository(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task<IReadOnlyList<ClinicaHeridasSeguimientoRow>> GetSeguimientosPorCarpetaAsync(
        string carpetaDriveItemId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(carpetaDriveItemId))
        {
            return [];
        }

        await using var connection = new NpgsqlConnection(NeonConnectionString.FromConfiguration(_configuration));
        await connection.OpenAsync(cancellationToken);

        var seguimientos = new List<ClinicaHeridasSeguimientoRow>();
        var porId = new Dictionary<string, ClinicaHeridasSeguimientoRow>(StringComparer.Ordinal);

        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                select
                    s.id,
                    s.numero,
                    s."createdAt",
                    s.origen,
                    s.ubicacion,
                    s."diametroVerticalCm",
                    s."diametroHorizontalCm",
                    s."profundidadCm",
                    s.fondo,
                    s.lecho,
                    s.tejido,
                    s."cavitacionTunelizacion",
                    s."pielPerilesional",
                    s."exudadoCantidad",
                    s."exudadoCaracteristicas",
                    s."carpetaDriveItemId",
                    u.nombres,
                    u."primerApellido",
                    u."segundoApellido",
                    u.cedula,
                    u.email,
                    u.profesion::text as profesion
                from public."ClinicaHeridas" s
                join public."ClinicaHeridasPaciente" p on p."pacienteRef" = s."pacienteRef"
                left join public."User" u on u.id = s."usuarioId"
                where p."carpetaDriveItemId" = @carpeta
                order by s.numero desc;
                """;
            command.Parameters.AddWithValue("carpeta", carpetaDriveItemId);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var seguimiento = new ClinicaHeridasSeguimientoRow
                {
                    Id = GetString(reader, "id"),
                    Numero = reader.GetInt32(reader.GetOrdinal("numero")),
                    CreatedAtUtc = DateTime.SpecifyKind(
                        reader.GetDateTime(reader.GetOrdinal("createdAt")),
                        DateTimeKind.Utc),
                    Origen = GetString(reader, "origen"),
                    Ubicacion = GetString(reader, "ubicacion"),
                    DiametroVerticalCm = GetDouble(reader, "diametroVerticalCm"),
                    DiametroHorizontalCm = GetDouble(reader, "diametroHorizontalCm"),
                    ProfundidadCm = GetDouble(reader, "profundidadCm"),
                    Fondo = GetString(reader, "fondo"),
                    Lecho = GetString(reader, "lecho"),
                    Tejido = GetString(reader, "tejido"),
                    CavitacionTunelizacion = GetString(reader, "cavitacionTunelizacion"),
                    PielPerilesional = GetString(reader, "pielPerilesional"),
                    ExudadoCantidad = GetString(reader, "exudadoCantidad"),
                    ExudadoCaracteristicas = GetString(reader, "exudadoCaracteristicas"),
                    CarpetaDriveItemId = GetNullableString(reader, "carpetaDriveItemId"),
                    AuxiliarNombre = BuildFullName(
                        GetString(reader, "nombres"),
                        GetString(reader, "primerApellido"),
                        GetString(reader, "segundoApellido")),
                    AuxiliarCedula = GetString(reader, "cedula"),
                    AuxiliarEmail = GetString(reader, "email"),
                    AuxiliarProfesion = GetString(reader, "profesion")
                };

                seguimientos.Add(seguimiento);
                porId[seguimiento.Id] = seguimiento;
            }
        }

        if (seguimientos.Count == 0)
        {
            return seguimientos;
        }

        var fotosPorSeguimiento = new Dictionary<string, List<ClinicaHeridasFotoRow>>(StringComparer.Ordinal);

        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                select
                    f."seguimientoId",
                    f.tipo::text as tipo,
                    f."driveItemId",
                    f.nombre,
                    f."mimeType",
                    f."createdAt"
                from public."ClinicaHeridasFoto" f
                where f."seguimientoId" = any(@seguimientos)
                order by f."createdAt";
                """;
            command.Parameters.AddWithValue("seguimientos", seguimientos.Select(x => x.Id).ToArray());

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var seguimientoId = GetString(reader, "seguimientoId");
                if (!fotosPorSeguimiento.TryGetValue(seguimientoId, out var fotos))
                {
                    fotos = [];
                    fotosPorSeguimiento[seguimientoId] = fotos;
                }

                fotos.Add(new ClinicaHeridasFotoRow
                {
                    Tipo = GetString(reader, "tipo"),
                    DriveItemId = GetString(reader, "driveItemId"),
                    Nombre = GetString(reader, "nombre"),
                    MimeType = GetNullableString(reader, "mimeType"),
                    CreatedAtUtc = DateTime.SpecifyKind(
                        reader.GetDateTime(reader.GetOrdinal("createdAt")),
                        DateTimeKind.Utc)
                });
            }
        }

        foreach (var (seguimientoId, fotos) in fotosPorSeguimiento)
        {
            if (porId.TryGetValue(seguimientoId, out var seguimiento))
            {
                seguimiento.Fotos = fotos;
            }
        }

        return seguimientos;
    }

    public async Task<ClinicaHeridasFotoRow?> GetFotoPorDriveItemIdAsync(
        string driveItemId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(driveItemId))
        {
            return null;
        }

        await using var connection = new NpgsqlConnection(NeonConnectionString.FromConfiguration(_configuration));
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            select
                f.tipo::text as tipo,
                f."driveItemId",
                f.nombre,
                f."mimeType",
                f."createdAt"
            from public."ClinicaHeridasFoto" f
            where f."driveItemId" = @driveItemId
            limit 1;
            """;
        command.Parameters.AddWithValue("driveItemId", driveItemId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new ClinicaHeridasFotoRow
        {
            Tipo = GetString(reader, "tipo"),
            DriveItemId = GetString(reader, "driveItemId"),
            Nombre = GetString(reader, "nombre"),
            MimeType = GetNullableString(reader, "mimeType"),
            CreatedAtUtc = DateTime.SpecifyKind(
                reader.GetDateTime(reader.GetOrdinal("createdAt")),
                DateTimeKind.Utc)
        };
    }

    private static string BuildFullName(string nombres, string primerApellido, string segundoApellido)
    {
        var partes = new[] { nombres, primerApellido, segundoApellido }
            .Where(value => !string.IsNullOrWhiteSpace(value));
        return string.Join(' ', partes).Trim();
    }

    private static string GetString(NpgsqlDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? string.Empty : Convert.ToString(reader.GetValue(ordinal)) ?? string.Empty;
    }

    private static string? GetNullableString(NpgsqlDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : Convert.ToString(reader.GetValue(ordinal));
    }

    private static double GetDouble(NpgsqlDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? 0d : reader.GetDouble(ordinal);
    }
}
