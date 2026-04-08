using ApprovalDemo.Api.Models;
using Npgsql;
using NpgsqlTypes;

namespace ApprovalDemo.Api.Data
{
    public sealed class StudentRepository
    {
        private readonly string _connectionString;

        public StudentRepository(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
        }

        public async Task<IReadOnlyList<string>> GetGradesAsync(string? search, int limit, CancellationToken cancellationToken)
        {
            var grades = new List<string>();

            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            const string sql = @"
SELECT DISTINCT ""GradeName""
FROM ""StudentDirectory""
WHERE (@Search IS NULL OR ""GradeName"" ILIKE CONCAT('%', @Search, '%'))
  AND COALESCE(""IsActive"", TRUE) = TRUE
ORDER BY ""GradeName""
LIMIT @Limit";

            await using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("@Search", (object?)Normalize(search) ?? DBNull.Value);
            command.Parameters.AddWithValue("@Limit", limit);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                grades.Add(reader.GetString(0));
            }

            return grades;
        }

        public async Task<IReadOnlyList<string>> GetSectionsAsync(string? grade, string? search, int limit, CancellationToken cancellationToken)
        {
            var sections = new List<string>();

            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            const string sql = @"
SELECT DISTINCT ""SectionName""
FROM ""StudentDirectory""
WHERE (@Grade IS NULL OR ""GradeName"" = @Grade)
  AND (@Search IS NULL OR ""SectionName"" ILIKE CONCAT('%', @Search, '%'))
  AND COALESCE(""IsActive"", TRUE) = TRUE
ORDER BY ""SectionName""
LIMIT @Limit";

            await using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("@Grade", (object?)Normalize(grade) ?? DBNull.Value);
            command.Parameters.AddWithValue("@Search", (object?)Normalize(search) ?? DBNull.Value);
            command.Parameters.AddWithValue("@Limit", limit);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                sections.Add(reader.GetString(0));
            }

            return sections;
        }

        public async Task<PagedResult<StudentDirectoryItem>> GetStudentsAsync(StudentDirectoryQuery query, CancellationToken cancellationToken)
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            const string countSql = @"
SELECT COUNT(*)
FROM ""StudentDirectory""
WHERE (@Grade IS NULL OR ""GradeName"" = @Grade)
  AND (@Section IS NULL OR ""SectionName"" = @Section)
  AND (@Search IS NULL OR ""FullName"" ILIKE CONCAT('%', @Search, '%') OR ""StudentCode"" ILIKE CONCAT('%', @Search, '%'))
  AND (@OnlyActive = FALSE OR COALESCE(""IsActive"", TRUE) = TRUE)";

            await using var countCommand = new NpgsqlCommand(countSql, connection);
            AddQueryParameters(countCommand, query);
            var total = Convert.ToInt32(await countCommand.ExecuteScalarAsync(cancellationToken));

            const string listSql = @"
SELECT ""Id"", ""StudentCode"", ""FullName"", ""GradeName"", ""SectionName"", ""PhotoUrl"", COALESCE(""IsActive"", TRUE) AS ""IsActive""
FROM ""StudentDirectory""
WHERE (@Grade IS NULL OR ""GradeName"" = @Grade)
  AND (@Section IS NULL OR ""SectionName"" = @Section)
  AND (@Search IS NULL OR ""FullName"" ILIKE CONCAT('%', @Search, '%') OR ""StudentCode"" ILIKE CONCAT('%', @Search, '%'))
  AND (@OnlyActive = FALSE OR COALESCE(""IsActive"", TRUE) = TRUE)
ORDER BY ""FullName""
LIMIT @PageSize OFFSET @Offset";

            await using var listCommand = new NpgsqlCommand(listSql, connection);
            AddQueryParameters(listCommand, query);
            listCommand.Parameters.AddWithValue("@Offset", (query.Page - 1) * query.PageSize);

            var items = new List<StudentDirectoryItem>();
            await using var reader = await listCommand.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                items.Add(new StudentDirectoryItem
                {
                    Id = reader.GetInt32(reader.GetOrdinal("Id")),
                    StudentCode = reader.GetString(reader.GetOrdinal("StudentCode")),
                    FullName = reader.GetString(reader.GetOrdinal("FullName")),
                    GradeName = reader.GetString(reader.GetOrdinal("GradeName")),
                    SectionName = reader.GetString(reader.GetOrdinal("SectionName")),
                    PhotoUrl = reader.IsDBNull(reader.GetOrdinal("PhotoUrl")) ? null : reader.GetString(reader.GetOrdinal("PhotoUrl")),
                    IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive"))
                });
            }

            return new PagedResult<StudentDirectoryItem>
            {
                Items = items,
                Total = total,
                Page = query.Page,
                PageSize = query.PageSize
            };
        }

        public async Task<IReadOnlyList<StudentDirectoryItem>> GetStudentsByIdsAsync(IReadOnlyCollection<int> ids, CancellationToken cancellationToken)
        {
            if (ids.Count == 0)
            {
                return Array.Empty<StudentDirectoryItem>();
            }

            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            const string sql = @"
SELECT ""Id"", ""StudentCode"", ""FullName"", ""GradeName"", ""SectionName"", ""PhotoUrl"", COALESCE(""IsActive"", TRUE) AS ""IsActive""
FROM ""StudentDirectory""
WHERE ""Id"" = ANY(@Ids)
ORDER BY ""FullName""";

            await using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.Add("@Ids", NpgsqlDbType.Array | NpgsqlDbType.Integer).Value = ids.ToArray();

            var items = new List<StudentDirectoryItem>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                items.Add(new StudentDirectoryItem
                {
                    Id = reader.GetInt32(reader.GetOrdinal("Id")),
                    StudentCode = reader.GetString(reader.GetOrdinal("StudentCode")),
                    FullName = reader.GetString(reader.GetOrdinal("FullName")),
                    GradeName = reader.GetString(reader.GetOrdinal("GradeName")),
                    SectionName = reader.GetString(reader.GetOrdinal("SectionName")),
                    PhotoUrl = reader.IsDBNull(reader.GetOrdinal("PhotoUrl")) ? null : reader.GetString(reader.GetOrdinal("PhotoUrl")),
                    IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive"))
                });
            }

            return items;
        }

        private static void AddQueryParameters(NpgsqlCommand command, StudentDirectoryQuery query)
        {
            command.Parameters.AddWithValue("@Grade", (object?)Normalize(query.Grade) ?? DBNull.Value);
            command.Parameters.AddWithValue("@Section", (object?)Normalize(query.Section) ?? DBNull.Value);
            command.Parameters.AddWithValue("@Search", (object?)Normalize(query.Search) ?? DBNull.Value);
            command.Parameters.AddWithValue("@OnlyActive", query.OnlyActive);
            command.Parameters.AddWithValue("@PageSize", query.PageSize);
        }

        private static string? Normalize(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
    }
}
