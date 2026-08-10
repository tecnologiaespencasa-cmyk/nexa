using Nexa.Data.Repositories.Interfaces;
using Nexa.Data.Repositories.Models;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace Nexa.Data.Repositories;

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
        return NeonConnectionString.FromConfiguration(_configuration);
    }

    private static string GetString(NpgsqlDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? string.Empty : Convert.ToString(reader.GetValue(ordinal)) ?? string.Empty;
    }
}
