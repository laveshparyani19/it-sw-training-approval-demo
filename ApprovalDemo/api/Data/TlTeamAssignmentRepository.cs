using ApprovalDemo.Api.Models;
using Npgsql;
using NpgsqlTypes;

namespace ApprovalDemo.Api.Data
{
    public sealed class TlTeamAssignmentRepository
    {
        private readonly string _connectionString;

        public TlTeamAssignmentRepository(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
        }

        public async Task EnsureSchemaAsync(CancellationToken cancellationToken)
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            const string ddl = @"
CREATE TABLE IF NOT EXISTS ""TlTeamAssignment"" (
    ""Id"" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    ""TlStaffCode"" VARCHAR(50) NOT NULL,
    ""DepartmentName"" VARCHAR(100) NOT NULL,
    ""TeamName"" VARCHAR(100) NOT NULL,
    ""MemberStaffIds"" INTEGER[] NOT NULL,
    ""TaskDescription"" TEXT NULL,
    ""CreatedAt"" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    ""UpdatedAt"" TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS ""IX_TlTeamAssignment_TlStaffCode""
    ON ""TlTeamAssignment"" (""TlStaffCode"");

CREATE INDEX IF NOT EXISTS ""IX_TlTeamAssignment_Dept_Team""
    ON ""TlTeamAssignment"" (""DepartmentName"", ""TeamName"");";

            await using var command = new NpgsqlCommand(ddl, connection);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        public async Task<TlTeamAssignmentItem> CreateAsync(CreateTlTeamAssignmentDto dto, CancellationToken cancellationToken)
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            const string sql = @"
INSERT INTO ""TlTeamAssignment"" (
    ""TlStaffCode"", ""DepartmentName"", ""TeamName"", ""MemberStaffIds"", ""TaskDescription"", ""CreatedAt"", ""UpdatedAt"")
VALUES (@TlStaffCode, @DepartmentName, @TeamName, @MemberStaffIds, @TaskDescription, NOW(), NOW())
RETURNING ""Id"", ""TlStaffCode"", ""DepartmentName"", ""TeamName"", ""MemberStaffIds"", ""TaskDescription"", ""CreatedAt"", ""UpdatedAt"";";

            await using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("@TlStaffCode", dto.TlStaffCode.Trim());
            command.Parameters.AddWithValue("@DepartmentName", dto.DepartmentName.Trim());
            command.Parameters.AddWithValue("@TeamName", dto.TeamName.Trim());
            command.Parameters.Add("@MemberStaffIds", NpgsqlDbType.Array | NpgsqlDbType.Integer).Value = dto.MemberStaffIds;
            command.Parameters.Add("@TaskDescription", NpgsqlDbType.Text).Value = (object?)dto.TaskDescription?.Trim() ?? DBNull.Value;

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                throw new InvalidOperationException("Insert did not return a row.");
            }

            return ReadItem(reader);
        }

        public async Task<IReadOnlyList<TlTeamAssignmentItem>> ListRecentByTlAsync(string tlStaffCode, int take, CancellationToken cancellationToken)
        {
            var list = new List<TlTeamAssignmentItem>();
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            const string sql = @"
SELECT ""Id"", ""TlStaffCode"", ""DepartmentName"", ""TeamName"", ""MemberStaffIds"", ""TaskDescription"", ""CreatedAt"", ""UpdatedAt""
FROM ""TlTeamAssignment""
WHERE ""TlStaffCode"" = @TlStaffCode
ORDER BY ""CreatedAt"" DESC
LIMIT @Take";

            await using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("@TlStaffCode", tlStaffCode.Trim());
            command.Parameters.AddWithValue("@Take", Math.Clamp(take, 1, 100));

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                list.Add(ReadItem(reader));
            }

            return list;
        }

        public async Task<bool> StaffIdsExistAndMatchTeamAsync(
            IReadOnlyCollection<int> staffIds,
            string departmentName,
            string teamName,
            CancellationToken cancellationToken)
        {
            if (staffIds.Count == 0)
            {
                return false;
            }

            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            const string sql = @"
SELECT COUNT(*)::INT
FROM ""StaffDirectory""
WHERE ""Id"" = ANY(@Ids)
  AND ""DepartmentName"" = @DepartmentName
  AND ""TeamName"" = @TeamName
  AND COALESCE(""IsActive"", TRUE) = TRUE
  AND COALESCE(""IsSystemAccount"", FALSE) = FALSE";

            await using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.Add("@Ids", NpgsqlDbType.Array | NpgsqlDbType.Integer).Value = staffIds.ToArray();
            command.Parameters.AddWithValue("@DepartmentName", departmentName.Trim());
            command.Parameters.AddWithValue("@TeamName", teamName.Trim());

            var count = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
            return count == staffIds.Count;
        }

        private static TlTeamAssignmentItem ReadItem(NpgsqlDataReader reader)
        {
            var idOrdinal = reader.GetOrdinal("Id");
            var idsOrdinal = reader.GetOrdinal("MemberStaffIds");
            var taskDescOrdinal = reader.GetOrdinal("TaskDescription");

            int[] memberIds;
            if (reader.IsDBNull(idsOrdinal))
            {
                memberIds = Array.Empty<int>();
            }
            else
            {
                memberIds = (int[])reader.GetValue(idsOrdinal);
            }

            return new TlTeamAssignmentItem
            {
                Id = reader.GetGuid(idOrdinal),
                TlStaffCode = reader.GetString(reader.GetOrdinal("TlStaffCode")),
                DepartmentName = reader.GetString(reader.GetOrdinal("DepartmentName")),
                TeamName = reader.GetString(reader.GetOrdinal("TeamName")),
                MemberStaffIds = memberIds,
                TaskDescription = reader.IsDBNull(taskDescOrdinal) ? null : reader.GetString(taskDescOrdinal),
                CreatedAtUtc = DateTime.SpecifyKind(reader.GetDateTime(reader.GetOrdinal("CreatedAt")), DateTimeKind.Utc),
                UpdatedAtUtc = DateTime.SpecifyKind(reader.GetDateTime(reader.GetOrdinal("UpdatedAt")), DateTimeKind.Utc)
            };
        }
    }
}
