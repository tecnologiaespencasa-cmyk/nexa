using System.Web;
using IntranetPrueba.Data.Repositories.Interfaces;
using IntranetPrueba.Data.Repositories.Models;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace IntranetPrueba.Data.Repositories;

public class NeonOpsAssistantUserRepository : INeonOpsAssistantUserRepository
{
    private readonly IConfiguration _configuration;

    public NeonOpsAssistantUserRepository(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task<IReadOnlyList<NeonOpsAssistantUserRow>> GetUsersAsync(
        bool onlyActive,
        CancellationToken cancellationToken = default)
    {
        var connectionString = GetConnectionString();
        var users = new List<NeonOpsAssistantUserRow>();

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            select
                activo,
                email,
                nombres,
                "primerApellido",
                "segundoApellido",
                telefono,
                cedula,
                profesion::text as profesion
            from public."User"
            where (@onlyActive = false or activo = true)
            order by nombres, "primerApellido", "segundoApellido";
            """;
        command.Parameters.AddWithValue("onlyActive", onlyActive);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            users.Add(new NeonOpsAssistantUserRow
            {
                IsActive = !reader.IsDBNull(reader.GetOrdinal("activo")) && reader.GetBoolean(reader.GetOrdinal("activo")),
                Email = GetString(reader, "email"),
                FirstName = GetString(reader, "nombres"),
                LastName1 = GetString(reader, "primerApellido"),
                LastName2 = GetString(reader, "segundoApellido"),
                Phone = GetString(reader, "telefono"),
                NationalId = GetString(reader, "cedula"),
                Profession = GetString(reader, "profesion")
            });
        }

        return users;
    }

    private string GetConnectionString()
    {
        var connectionString = _configuration["DATABASE_URL"]
            ?? _configuration["OpsAssistantDirectory:ConnectionString"];

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "DATABASE_URL no está configurada para leer los auxiliares OPS desde Neon.");
        }

        if (connectionString.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase)
            || connectionString.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
        {
            return ConvertDatabaseUrl(connectionString);
        }

        return connectionString;
    }

    private static string ConvertDatabaseUrl(string databaseUrl)
    {
        var uri = new Uri(databaseUrl);
        var userInfoParts = uri.UserInfo.Split(':', 2);
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.Port > 0 ? uri.Port : 5432,
            Database = uri.AbsolutePath.TrimStart('/'),
            Username = Uri.UnescapeDataString(userInfoParts.ElementAtOrDefault(0) ?? string.Empty),
            Password = Uri.UnescapeDataString(userInfoParts.ElementAtOrDefault(1) ?? string.Empty),
            SslMode = SslMode.Require
        };

        var query = HttpUtility.ParseQueryString(uri.Query);
        if (query["sslmode"] is { Length: > 0 } sslMode
            && Enum.TryParse<SslMode>(sslMode, ignoreCase: true, out var parsedSslMode))
        {
            builder.SslMode = parsedSslMode;
        }

        if (query["channel_binding"] is { Length: > 0 } channelBinding)
        {
            builder["Channel Binding"] = channelBinding;
        }

        return builder.ConnectionString;
    }

    private static string GetString(NpgsqlDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? string.Empty : Convert.ToString(reader.GetValue(ordinal)) ?? string.Empty;
    }
}
