using System.Net;
using Microsoft.Extensions.Configuration;

namespace ApprovalDemo.Api.Configuration
{
    /// <summary>
    /// Resolves Postgres (Supabase) and optional MSSQL connection strings from environment variables
    /// and configuration — shared by the web API and the hybrid sync worker.
    /// </summary>
    public static class DatabaseConnectionResolver
    {
        public static string ResolvePostgresConnectionString(IConfiguration configuration)
        {
            var rawConnectionString = Environment.GetEnvironmentVariable("SUPABASE_CONNECTION_STRING")
                ?? Environment.GetEnvironmentVariable("DATABASE_URL")
                ?? configuration["SUPABASE_CONNECTION_STRING"];

            var configuredDefaultConnection = configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrWhiteSpace(rawConnectionString)
                && !string.IsNullOrWhiteSpace(configuredDefaultConnection)
                && !string.Equals(configuredDefaultConnection, "DefaultConnection", StringComparison.OrdinalIgnoreCase))
            {
                rawConnectionString = configuredDefaultConnection;
            }

            if (!string.IsNullOrWhiteSpace(rawConnectionString))
            {
                return rawConnectionString.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase)
                    || rawConnectionString.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase)
                    ? BuildNpgsqlConnectionStringFromUri(rawConnectionString)
                    : rawConnectionString;
            }

            var supabasePassword = Environment.GetEnvironmentVariable("SUPABASE_PASSWORD")
                ?? configuration["SUPABASE_PASSWORD"]
                ?? throw new InvalidOperationException(
                    "Set SUPABASE_CONNECTION_STRING (recommended) or SUPABASE_PASSWORD for Postgres access.");
            var host = Environment.GetEnvironmentVariable("SUPABASE_HOST") ?? "db.qxevtcviybjzqueipukf.supabase.co";
            var port = Environment.GetEnvironmentVariable("SUPABASE_PORT") ?? "5432";
            var username = Environment.GetEnvironmentVariable("SUPABASE_USER") ?? "postgres";
            var database = Environment.GetEnvironmentVariable("SUPABASE_DB") ?? "postgres";

            return $"Host={host};Port={port};Username={username};Password={supabasePassword};Database={database};SSL Mode=Require;Trust Server Certificate=true";
        }

        public static string? ResolveMssqlConnectionString(IConfiguration configuration)
        {
            return Environment.GetEnvironmentVariable("MSSQL_CONNECTION_STRING")
                ?? configuration["ApprovalDemoDbConnectionString"]
                ?? configuration.GetConnectionString("MssqlReporting");
        }

        public static string BuildNpgsqlConnectionStringFromUri(string uriString)
        {
            var uri = new Uri(uriString);

            var userInfo = uri.UserInfo.Split(':', 2);
            if (userInfo.Length != 2)
            {
                throw new InvalidOperationException("Invalid Postgres URI format. Expected username and password in the URI.");
            }

            var username = WebUtility.UrlDecode(userInfo[0]);
            var password = WebUtility.UrlDecode(userInfo[1]);
            var database = uri.AbsolutePath.Trim('/');
            if (string.IsNullOrWhiteSpace(database))
            {
                database = "postgres";
            }

            return $"Host={uri.Host};Port={uri.Port};Username={username};Password={password};Database={database};SSL Mode=Require;Trust Server Certificate=true";
        }
    }
}
