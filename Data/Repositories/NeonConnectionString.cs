using System.Web;
using Npgsql;

namespace Nexa.Data.Repositories;

internal static class NeonConnectionString
{
    public static string FromConfiguration(IConfiguration configuration)
    {
        var connectionString = configuration["DATABASE_URL"]
            ?? configuration["OpsAssistantDirectory:ConnectionString"];

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "DATABASE_URL no está configurada para leer datos desde Portal Administrativo.");
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
}
