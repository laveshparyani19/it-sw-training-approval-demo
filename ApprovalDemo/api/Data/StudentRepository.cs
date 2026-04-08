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

        public async Task EnsureSchemaAndSeedAsync(CancellationToken cancellationToken)
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            const string ddl = @"
CREATE TABLE IF NOT EXISTS ""StudentDirectory"" (
    ""Id"" SERIAL PRIMARY KEY,
    ""StudentCode"" VARCHAR(50) NOT NULL UNIQUE,
    ""FullName"" VARCHAR(200) NOT NULL,
    ""GradeName"" VARCHAR(50) NOT NULL,
    ""SectionName"" VARCHAR(50) NOT NULL,
    ""PhotoUrl"" TEXT NULL,
    ""IsActive"" BOOLEAN NOT NULL DEFAULT TRUE,
    ""UpdatedAt"" TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS ""IX_StudentDirectory_Grade_Section""
ON ""StudentDirectory"" (""GradeName"", ""SectionName"");

CREATE INDEX IF NOT EXISTS ""IX_StudentDirectory_Active_FullName""
ON ""StudentDirectory"" (""IsActive"", ""FullName"");";

            await using (var ddlCommand = new NpgsqlCommand(ddl, connection))
            {
                await ddlCommand.ExecuteNonQueryAsync(cancellationToken);
            }

            const string countSql = "SELECT COUNT(*) FROM \"StudentDirectory\"";
            await using var countCommand = new NpgsqlCommand(countSql, connection);
            var count = Convert.ToInt32(await countCommand.ExecuteScalarAsync(cancellationToken));
            if (count > 0)
            {
                return;
            }

            const string seedSql = @"
INSERT INTO ""StudentDirectory"" (""StudentCode"", ""FullName"", ""GradeName"", ""SectionName"", ""PhotoUrl"", ""IsActive"")
VALUES
('FSK2019011', 'Aarav Shah', 'Grade 5', 'Mavericks - 5', 'https://i.pravatar.cc/120?img=11', TRUE),
('FSK2020534', 'Aadhyan Khunt', 'Grade 11', 'Mavericks - 11', 'https://i.pravatar.cc/120?img=12', TRUE),
('FSK2023002', 'Aaditya Unhad', 'Grade 12', 'Mavericks - 12', 'https://i.pravatar.cc/120?img=13', TRUE),
('FSK2023201', 'Aadish Patil', 'Grade 11', 'Aplomb', 'https://i.pravatar.cc/120?img=14', TRUE),
('FSK2025303', 'Aadvik Dhamija', 'Grade 12', 'Equanimity', 'https://i.pravatar.cc/120?img=15', TRUE),
('FSK2024001', 'Aadya Bohra', 'Grade 10', 'G10', 'https://i.pravatar.cc/120?img=16', TRUE),
('FSK2017017', 'Aadya Chawla', 'Grade 9', 'G9', 'https://i.pravatar.cc/120?img=17', TRUE),
('FSK2020601', 'Aadya Jain', 'Grade 8', 'G8', 'https://i.pravatar.cc/120?img=18', TRUE),
('FSK2015171', 'Aadya Mittal', 'Grade 7', 'G7', 'https://i.pravatar.cc/120?img=19', TRUE),
('FSK2020042', 'Aadya Shah', 'Grade 6', 'G6', 'https://i.pravatar.cc/120?img=20', TRUE),
('FSK2023003', 'Aaeesha Malik', 'Grade 5', 'Collaboration', 'https://i.pravatar.cc/120?img=21', TRUE),
('FSK2016208', 'Aagam Chhalani', 'Grade 4', 'G4', 'https://i.pravatar.cc/120?img=22', TRUE),
('FSK2023219', 'Aagney Patel', 'Grade 3', 'G3', 'https://i.pravatar.cc/120?img=23', TRUE),
('FSK2023526', 'Aadhya Bhawakar', 'Grade 2', 'G2', 'https://i.pravatar.cc/120?img=24', TRUE),
('FSK2018045', 'Aadhya Gambhir', 'Grade 1', 'G1', 'https://i.pravatar.cc/120?img=25', TRUE),
('FSK2019231', 'Aadhya Pandya', 'Grade 5', 'Collaboration', 'https://i.pravatar.cc/120?img=26', TRUE),
('FSK2019051', 'Aadhya Patel', 'Grade 5', 'Mavericks - 5', 'https://i.pravatar.cc/120?img=27', TRUE),
('FSK2025004', 'Aadit Thakkar', 'Grade 12', 'Mavericks - 12', 'https://i.pravatar.cc/120?img=28', TRUE),
('FSK2023001', 'Aadit Patel', 'Grade 11', 'Mavericks - 11', 'https://i.pravatar.cc/120?img=29', TRUE),
('FSK2019001', 'Aaditya Maru', 'Grade 12', 'Sangfroid', 'https://i.pravatar.cc/120?img=30', TRUE)
ON CONFLICT (""StudentCode"") DO NOTHING;";

            await using var seedCommand = new NpgsqlCommand(seedSql, connection);
            await seedCommand.ExecuteNonQueryAsync(cancellationToken);
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
            command.Parameters.Add("@Search", NpgsqlDbType.Text).Value = (object?)Normalize(search) ?? DBNull.Value;
            command.Parameters.AddWithValue("@Limit", limit);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                grades.Add(reader.GetString(0));
            }

            return grades;
        }

        public async Task<IReadOnlyList<string>> GetSectionsAsync(IReadOnlyCollection<string> grades, string? search, int limit, CancellationToken cancellationToken)
        {
            var sections = new List<string>();

            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            const string sql = @"
SELECT DISTINCT ""SectionName""
FROM ""StudentDirectory""
            WHERE (@GradesEmpty = TRUE OR ""GradeName"" = ANY(@Grades))
  AND (@Search IS NULL OR ""SectionName"" ILIKE CONCAT('%', @Search, '%'))
  AND COALESCE(""IsActive"", TRUE) = TRUE
ORDER BY ""SectionName""
LIMIT @Limit";

            await using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("@GradesEmpty", grades.Count == 0);
            command.Parameters.Add("@Grades", NpgsqlDbType.Array | NpgsqlDbType.Text).Value = grades.ToArray();
            command.Parameters.Add("@Search", NpgsqlDbType.Text).Value = (object?)Normalize(search) ?? DBNull.Value;
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
            var grades = ParseCsv(query.Grades);
            if (grades.Count == 0 && !string.IsNullOrWhiteSpace(query.Grade))
            {
                grades.Add(query.Grade.Trim());
            }

            var sections = ParseCsv(query.Sections);
            if (sections.Count == 0 && !string.IsNullOrWhiteSpace(query.Section))
            {
                sections.Add(query.Section.Trim());
            }

            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            const string countSql = @"
SELECT COUNT(*)
FROM ""StudentDirectory""
WHERE (@GradesEmpty = TRUE OR ""GradeName"" = ANY(@Grades))
    AND (@SectionsEmpty = TRUE OR ""SectionName"" = ANY(@Sections))
  AND (@Search IS NULL OR ""FullName"" ILIKE CONCAT('%', @Search, '%') OR ""StudentCode"" ILIKE CONCAT('%', @Search, '%'))
  AND (@OnlyActive = FALSE OR COALESCE(""IsActive"", TRUE) = TRUE)";

            await using var countCommand = new NpgsqlCommand(countSql, connection);
            AddQueryParameters(countCommand, query, grades, sections);
            var total = Convert.ToInt32(await countCommand.ExecuteScalarAsync(cancellationToken));

            const string listSql = @"
SELECT ""Id"", ""StudentCode"", ""FullName"", ""GradeName"", ""SectionName"", ""PhotoUrl"", COALESCE(""IsActive"", TRUE) AS ""IsActive""
FROM ""StudentDirectory""
WHERE (@GradesEmpty = TRUE OR ""GradeName"" = ANY(@Grades))
    AND (@SectionsEmpty = TRUE OR ""SectionName"" = ANY(@Sections))
  AND (@Search IS NULL OR ""FullName"" ILIKE CONCAT('%', @Search, '%') OR ""StudentCode"" ILIKE CONCAT('%', @Search, '%'))
  AND (@OnlyActive = FALSE OR COALESCE(""IsActive"", TRUE) = TRUE)
ORDER BY ""FullName""
LIMIT @PageSize OFFSET @Offset";

            await using var listCommand = new NpgsqlCommand(listSql, connection);
            AddQueryParameters(listCommand, query, grades, sections);
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

        private static void AddQueryParameters(NpgsqlCommand command, StudentDirectoryQuery query, IReadOnlyCollection<string> grades, IReadOnlyCollection<string> sections)
        {
            command.Parameters.AddWithValue("@GradesEmpty", grades.Count == 0);
            command.Parameters.Add("@Grades", NpgsqlDbType.Array | NpgsqlDbType.Text).Value = grades.ToArray();
            command.Parameters.AddWithValue("@SectionsEmpty", sections.Count == 0);
            command.Parameters.Add("@Sections", NpgsqlDbType.Array | NpgsqlDbType.Text).Value = sections.ToArray();
            command.Parameters.Add("@Search", NpgsqlDbType.Text).Value = (object?)Normalize(query.Search) ?? DBNull.Value;
            command.Parameters.AddWithValue("@OnlyActive", query.OnlyActive);
            command.Parameters.AddWithValue("@PageSize", query.PageSize);
        }

        private static List<string> ParseCsv(string? csv)
        {
            return (csv ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(static value => value.Trim())
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static string? Normalize(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
    }
}
