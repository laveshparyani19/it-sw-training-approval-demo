using ApprovalDemo.Api.Models;
using Npgsql;
using NpgsqlTypes;

namespace ApprovalDemo.Api.Data
{
    public sealed class StaffRepository
    {
        private readonly string _connectionString;

        public StaffRepository(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
        }

        public async Task EnsureSchemaAndSeedAsync(CancellationToken cancellationToken)
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            const string ddl = @"
CREATE TABLE IF NOT EXISTS ""StaffDirectory"" (
    ""Id"" SERIAL PRIMARY KEY,
    ""StaffCode"" VARCHAR(50) NOT NULL UNIQUE,
    ""FullName"" VARCHAR(200) NOT NULL,
    ""DepartmentName"" VARCHAR(100) NOT NULL,
    ""TeamName"" VARCHAR(100) NOT NULL,
    ""Designation"" VARCHAR(120) NOT NULL,
    ""PhotoUrl"" TEXT NULL,
    ""IsActive"" BOOLEAN NOT NULL DEFAULT TRUE,
    ""IsSystemAccount"" BOOLEAN NOT NULL DEFAULT FALSE,
    ""UpdatedAt"" TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS ""IX_StaffDirectory_Active_System_FullName""
ON ""StaffDirectory"" (""IsActive"", ""IsSystemAccount"", ""FullName"");

CREATE INDEX IF NOT EXISTS ""IX_StaffDirectory_Department_Team""
ON ""StaffDirectory"" (""DepartmentName"", ""TeamName"");

ALTER TABLE ""StaffDirectory"" ADD COLUMN IF NOT EXISTS ""StaffCategory"" VARCHAR(30) NOT NULL DEFAULT 'Academic';";

            await using (var ddlCommand = new NpgsqlCommand(ddl, connection))
            {
                await ddlCommand.ExecuteNonQueryAsync(cancellationToken);
            }

            const string countSql = "SELECT COUNT(*) FROM \"StaffDirectory\"";
            await using var countCommand = new NpgsqlCommand(countSql, connection);
            var count = Convert.ToInt32(await countCommand.ExecuteScalarAsync(cancellationToken));
            if (count > 0)
            {
                return;
            }

            const string seedSql = @"
INSERT INTO ""StaffDirectory"" (""StaffCode"", ""FullName"", ""DepartmentName"", ""TeamName"", ""Designation"", ""PhotoUrl"", ""IsActive"", ""IsSystemAccount"")
VALUES
('STF-1001', 'Lavesh Paryani', 'Academics', 'Grade 7', 'Teacher', 'https://i.pravatar.cc/120?img=61', TRUE, FALSE),
('STF-1002', 'Lubaina Faizullabhai', 'Academics', 'Grade 6', 'Teacher', 'https://i.pravatar.cc/120?img=62', TRUE, FALSE),
('STF-1003', 'Lulua Bandookwala', 'Academics', 'Grade 5', 'Teacher', 'https://i.pravatar.cc/120?img=63', TRUE, FALSE),
('STF-1004', 'Maheen Khan', 'Academics', 'Math', 'Teacher', 'https://i.pravatar.cc/120?img=64', TRUE, FALSE),
('STF-1005', 'Mahek Kothari', 'Academics', 'Science', 'Teacher', 'https://i.pravatar.cc/120?img=65', TRUE, FALSE),
('STF-1006', 'Mahmood Yacoobali', 'Operations', 'Transport', 'Coordinator', 'https://i.pravatar.cc/120?img=66', TRUE, FALSE),
('STF-1007', 'Malhar Trivedi', 'Technology', 'IT', 'Administrator', 'https://i.pravatar.cc/120?img=67', TRUE, FALSE),
('STF-1008', 'Manish Tiwari', 'Technology', 'IT', 'Support Engineer', 'https://i.pravatar.cc/120?img=68', TRUE, FALSE),
('STF-1009', 'Manisha Guha', 'Academics', 'English', 'Teacher', 'https://i.pravatar.cc/120?img=69', TRUE, FALSE),
('STF-1010', 'Manisha Naik', 'Academics', 'Primary', 'Teacher', 'https://i.pravatar.cc/120?img=70', TRUE, FALSE),
('STF-1011', 'Mayank Kapadia', 'Administration', 'Office', 'Manager', 'https://i.pravatar.cc/120?img=71', TRUE, FALSE),
('STF-1012', 'Mansi Dua', 'Administration', 'Office', 'Executive', 'https://i.pravatar.cc/120?img=72', TRUE, FALSE),
('STF-9999', 'SYSTEM ACCOUNT', 'System', 'Platform', 'Service User', NULL, TRUE, TRUE)
ON CONFLICT (""StaffCode"") DO NOTHING;";

            await using var seedCommand = new NpgsqlCommand(seedSql, connection);
            await seedCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<TeamOptionItem>> GetTeamOptionsAsync(int limit, CancellationToken cancellationToken)
        {
            var teams = new List<TeamOptionItem>();

            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            const string sql = @"
SELECT DISTINCT ""DepartmentName"", ""TeamName""
FROM ""StaffDirectory""
WHERE COALESCE(""IsActive"", TRUE) = TRUE
  AND COALESCE(""IsSystemAccount"", FALSE) = FALSE
ORDER BY ""DepartmentName"", ""TeamName""
LIMIT @Limit";

            await using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("@Limit", limit);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var dept = reader.GetString(0);
                var team = reader.GetString(1);
                teams.Add(new TeamOptionItem
                {
                    DepartmentName = dept,
                    TeamName = team,
                    DisplayLabel = $"{dept} - {team}"
                });
            }

            return teams;
        }

        public async Task<IReadOnlyList<string>> GetDepartmentsAsync(string? search, int limit, CancellationToken cancellationToken)
        {
            var departments = new List<string>();

            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            const string sql = @"
SELECT DISTINCT ""DepartmentName""
FROM ""StaffDirectory""
WHERE (@Search IS NULL OR ""DepartmentName"" ILIKE CONCAT('%', @Search, '%'))
  AND COALESCE(""IsActive"", TRUE) = TRUE
  AND COALESCE(""IsSystemAccount"", FALSE) = FALSE
ORDER BY ""DepartmentName""
LIMIT @Limit";

            await using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.Add("@Search", NpgsqlDbType.Text).Value = (object?)Normalize(search) ?? DBNull.Value;
            command.Parameters.AddWithValue("@Limit", limit);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                departments.Add(reader.GetString(0));
            }

            return departments;
        }

        public async Task<IReadOnlyList<string>> GetTeamsAsync(IReadOnlyCollection<string> departments, string? search, int limit, CancellationToken cancellationToken)
        {
            var teams = new List<string>();

            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            const string sql = @"
SELECT DISTINCT ""TeamName""
FROM ""StaffDirectory""
WHERE (@DepartmentsEmpty = TRUE OR ""DepartmentName"" = ANY(@Departments))
  AND (@Search IS NULL OR ""TeamName"" ILIKE CONCAT('%', @Search, '%'))
  AND COALESCE(""IsActive"", TRUE) = TRUE
  AND COALESCE(""IsSystemAccount"", FALSE) = FALSE
ORDER BY ""TeamName""
LIMIT @Limit";

            await using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("@DepartmentsEmpty", departments.Count == 0);
            command.Parameters.Add("@Departments", NpgsqlDbType.Array | NpgsqlDbType.Text).Value = departments.ToArray();
            command.Parameters.Add("@Search", NpgsqlDbType.Text).Value = (object?)Normalize(search) ?? DBNull.Value;
            command.Parameters.AddWithValue("@Limit", limit);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                teams.Add(reader.GetString(0));
            }

            return teams;
        }

        public async Task<PagedResult<StaffDirectoryItem>> GetStaffAsync(StaffDirectoryQuery query, CancellationToken cancellationToken)
        {
            var departments = ParseCsv(query.Departments);
            if (departments.Count == 0 && !string.IsNullOrWhiteSpace(query.Department))
            {
                departments.Add(query.Department.Trim());
            }

            var teams = ParseCsv(query.Teams);
            if (teams.Count == 0 && !string.IsNullOrWhiteSpace(query.Team))
            {
                teams.Add(query.Team.Trim());
            }

            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            const string countSql = @"
SELECT COUNT(*)
FROM ""StaffDirectory""
WHERE (@DepartmentsEmpty = TRUE OR ""DepartmentName"" = ANY(@Departments))
  AND (@TeamsEmpty = TRUE OR ""TeamName"" = ANY(@Teams))
  AND (@Search IS NULL OR ""FullName"" ILIKE CONCAT('%', @Search, '%') OR ""StaffCode"" ILIKE CONCAT('%', @Search, '%') OR ""Designation"" ILIKE CONCAT('%', @Search, '%'))
  AND (@OnlyActive = FALSE OR COALESCE(""IsActive"", TRUE) = TRUE)
  AND (@ExcludeSystemAccounts = FALSE OR COALESCE(""IsSystemAccount"", FALSE) = FALSE)";

            await using var countCommand = new NpgsqlCommand(countSql, connection);
            AddQueryParameters(countCommand, query, departments, teams);
            var total = Convert.ToInt32(await countCommand.ExecuteScalarAsync(cancellationToken));

            const string listSql = @"
SELECT ""Id"", ""StaffCode"", ""FullName"", ""DepartmentName"", ""TeamName"", ""Designation"", ""PhotoUrl"", COALESCE(""IsActive"", TRUE) AS ""IsActive"", COALESCE(""IsSystemAccount"", FALSE) AS ""IsSystemAccount""
FROM ""StaffDirectory""
WHERE (@DepartmentsEmpty = TRUE OR ""DepartmentName"" = ANY(@Departments))
  AND (@TeamsEmpty = TRUE OR ""TeamName"" = ANY(@Teams))
  AND (@Search IS NULL OR ""FullName"" ILIKE CONCAT('%', @Search, '%') OR ""StaffCode"" ILIKE CONCAT('%', @Search, '%') OR ""Designation"" ILIKE CONCAT('%', @Search, '%'))
  AND (@OnlyActive = FALSE OR COALESCE(""IsActive"", TRUE) = TRUE)
  AND (@ExcludeSystemAccounts = FALSE OR COALESCE(""IsSystemAccount"", FALSE) = FALSE)
ORDER BY ""FullName""
LIMIT @PageSize OFFSET @Offset";

            await using var listCommand = new NpgsqlCommand(listSql, connection);
            AddQueryParameters(listCommand, query, departments, teams);
            listCommand.Parameters.AddWithValue("@Offset", (query.Page - 1) * query.PageSize);

            var items = new List<StaffDirectoryItem>();
            await using var reader = await listCommand.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                items.Add(new StaffDirectoryItem
                {
                    Id = reader.GetInt32(reader.GetOrdinal("Id")),
                    StaffCode = reader.GetString(reader.GetOrdinal("StaffCode")),
                    FullName = reader.GetString(reader.GetOrdinal("FullName")),
                    DepartmentName = reader.GetString(reader.GetOrdinal("DepartmentName")),
                    TeamName = reader.GetString(reader.GetOrdinal("TeamName")),
                    Designation = reader.GetString(reader.GetOrdinal("Designation")),
                    PhotoUrl = reader.IsDBNull(reader.GetOrdinal("PhotoUrl")) ? null : reader.GetString(reader.GetOrdinal("PhotoUrl")),
                    IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive")),
                    IsSystemAccount = reader.GetBoolean(reader.GetOrdinal("IsSystemAccount"))
                });
            }

            return new PagedResult<StaffDirectoryItem>
            {
                Items = items,
                Total = total,
                Page = query.Page,
                PageSize = query.PageSize
            };
        }

        public async Task<IReadOnlyList<StaffDirectoryItem>> GetStaffByIdsAsync(IReadOnlyCollection<int> ids, bool excludeSystemAccounts, CancellationToken cancellationToken)
        {
            if (ids.Count == 0)
            {
                return Array.Empty<StaffDirectoryItem>();
            }

            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            const string sql = @"
SELECT ""Id"", ""StaffCode"", ""FullName"", ""DepartmentName"", ""TeamName"", ""Designation"", ""PhotoUrl"", COALESCE(""IsActive"", TRUE) AS ""IsActive"", COALESCE(""IsSystemAccount"", FALSE) AS ""IsSystemAccount""
FROM ""StaffDirectory""
WHERE ""Id"" = ANY(@Ids)
  AND (@ExcludeSystemAccounts = FALSE OR COALESCE(""IsSystemAccount"", FALSE) = FALSE)
ORDER BY ""FullName""";

            await using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.Add("@Ids", NpgsqlDbType.Array | NpgsqlDbType.Integer).Value = ids.ToArray();
            command.Parameters.AddWithValue("@ExcludeSystemAccounts", excludeSystemAccounts);

            var items = new List<StaffDirectoryItem>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                items.Add(new StaffDirectoryItem
                {
                    Id = reader.GetInt32(reader.GetOrdinal("Id")),
                    StaffCode = reader.GetString(reader.GetOrdinal("StaffCode")),
                    FullName = reader.GetString(reader.GetOrdinal("FullName")),
                    DepartmentName = reader.GetString(reader.GetOrdinal("DepartmentName")),
                    TeamName = reader.GetString(reader.GetOrdinal("TeamName")),
                    Designation = reader.GetString(reader.GetOrdinal("Designation")),
                    PhotoUrl = reader.IsDBNull(reader.GetOrdinal("PhotoUrl")) ? null : reader.GetString(reader.GetOrdinal("PhotoUrl")),
                    IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive")),
                    IsSystemAccount = reader.GetBoolean(reader.GetOrdinal("IsSystemAccount"))
                });
            }

            return items;
        }

        private static void AddQueryParameters(NpgsqlCommand command, StaffDirectoryQuery query, IReadOnlyCollection<string> departments, IReadOnlyCollection<string> teams)
        {
            command.Parameters.AddWithValue("@DepartmentsEmpty", departments.Count == 0);
            command.Parameters.Add("@Departments", NpgsqlDbType.Array | NpgsqlDbType.Text).Value = departments.ToArray();
            command.Parameters.AddWithValue("@TeamsEmpty", teams.Count == 0);
            command.Parameters.Add("@Teams", NpgsqlDbType.Array | NpgsqlDbType.Text).Value = teams.ToArray();
            command.Parameters.Add("@Search", NpgsqlDbType.Text).Value = (object?)Normalize(query.Search) ?? DBNull.Value;
            command.Parameters.AddWithValue("@OnlyActive", query.OnlyActive);
            command.Parameters.AddWithValue("@ExcludeSystemAccounts", query.ExcludeSystemAccounts);
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
