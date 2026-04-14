using System.Data;
using System.Linq;
using Microsoft.Data.SqlClient;
using Npgsql;

namespace ApprovalDemo.Api.Services;

public sealed partial class ApprovalSyncService
{
    private async Task<int> SyncTask8DataSnapshotAsync(CancellationToken cancellationToken)
    {
        if (!IsEnabled)
        {
            return 0;
        }

        var total = 0;
        total += await SyncAcademicProgramMirrorSnapshotAsync(cancellationToken);
        total += await SyncAcademicTermMirrorSnapshotAsync(cancellationToken);
        total += await SyncGradeSectionMentorMirrorSnapshotAsync(cancellationToken);
        total += await SyncHrisLeaveBalanceMirrorSnapshotAsync(cancellationToken);
        return total;
    }

    private async Task<int> SynchronizeTask8MirrorDeletesAsync(SqlConnection connection, CancellationToken cancellationToken)
    {
        var apSource = await GetAcademicProgramSourceIdsAsync(cancellationToken);
        var apTarget = await GetAcademicProgramTargetIdsAsync(cancellationToken);
        var apStale = apTarget.Where(id => !apSource.Contains(id)).ToArray();

        var termSource = await GetAcademicTermSourceIdsAsync(cancellationToken);
        var termTarget = await GetAcademicTermTargetIdsAsync(cancellationToken);
        var termStale = termTarget.Where(id => !termSource.Contains(id)).ToArray();

        var mentorSource = await GetGradeSectionMentorSourceIdsAsync(cancellationToken);
        var mentorTarget = await GetGradeSectionMentorTargetIdsAsync(cancellationToken);
        var mentorStale = mentorTarget.Where(id => !mentorSource.Contains(id)).ToArray();

        var leaveSource = await GetHrisLeaveBalanceSourceIdsAsync(cancellationToken);
        var leaveTarget = await GetHrisLeaveBalanceTargetIdsAsync(cancellationToken);
        var leaveStale = leaveTarget.Where(id => !leaveSource.Contains(id)).ToArray();

        var n = 0;
        n += await DeleteRowsByIdsAsync(connection, "DELETE FROM dbo.AcademicProgramMirror WHERE Id = @Id", apStale, cancellationToken);
        n += await DeleteRowsByIdsAsync(connection, "DELETE FROM dbo.AcademicTermMirror WHERE Id = @Id", termStale, cancellationToken);
        n += await DeleteRowsByIdsAsync(connection, "DELETE FROM dbo.GradeSectionMentorMirror WHERE Id = @Id", mentorStale, cancellationToken);
        n += await DeleteRowsByIdsAsync(connection, "DELETE FROM dbo.HrisLeaveBalanceMirror WHERE Id = @Id", leaveStale, cancellationToken);
        return n;
    }

    private async Task<int> SyncAcademicProgramMirrorSnapshotAsync(CancellationToken cancellationToken)
    {
        var rows = await GetAcademicProgramSourceRowsAsync(cancellationToken);
        if (rows.Count == 0)
        {
            return 0;
        }

        await using var connection = new SqlConnection(_mssqlConnectionString);
        await connection.OpenAsync(cancellationToken);
        foreach (var row in rows)
        {
            await UpsertAcademicProgramMirrorAsync(connection, row, cancellationToken);
        }

        return rows.Count;
    }

    private async Task<int> SyncAcademicTermMirrorSnapshotAsync(CancellationToken cancellationToken)
    {
        var rows = await GetAcademicTermSourceRowsAsync(cancellationToken);
        if (rows.Count == 0)
        {
            return 0;
        }

        await using var connection = new SqlConnection(_mssqlConnectionString);
        await connection.OpenAsync(cancellationToken);
        foreach (var row in rows)
        {
            await UpsertAcademicTermMirrorAsync(connection, row, cancellationToken);
        }

        return rows.Count;
    }

    private async Task<int> SyncGradeSectionMentorMirrorSnapshotAsync(CancellationToken cancellationToken)
    {
        var rows = await GetGradeSectionMentorSourceRowsAsync(cancellationToken);
        if (rows.Count == 0)
        {
            return 0;
        }

        await using var connection = new SqlConnection(_mssqlConnectionString);
        await connection.OpenAsync(cancellationToken);
        foreach (var row in rows)
        {
            await UpsertGradeSectionMentorMirrorAsync(connection, row, cancellationToken);
        }

        return rows.Count;
    }

    private async Task<int> SyncHrisLeaveBalanceMirrorSnapshotAsync(CancellationToken cancellationToken)
    {
        var rows = await GetHrisLeaveBalanceSourceRowsAsync(cancellationToken);
        if (rows.Count == 0)
        {
            return 0;
        }

        await using var connection = new SqlConnection(_mssqlConnectionString);
        await connection.OpenAsync(cancellationToken);
        foreach (var row in rows)
        {
            await UpsertHrisLeaveBalanceMirrorAsync(connection, row, cancellationToken);
        }

        return rows.Count;
    }

    private async Task<List<AcademicProgramSourceRow>> GetAcademicProgramSourceRowsAsync(CancellationToken cancellationToken)
    {
        var rows = new List<AcademicProgramSourceRow>();
        await using var connection = new NpgsqlConnection(_supabaseConnectionString);
        await connection.OpenAsync(cancellationToken);
        const string sql = @"
SELECT ""Id"", ""Code"", ""Name"", ""IsActive"", ""UpdatedAt""
FROM ""AcademicProgram""
ORDER BY ""Id"";";
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new AcademicProgramSourceRow
            {
                Id = reader.GetInt32(reader.GetOrdinal("Id")),
                Code = reader.GetString(reader.GetOrdinal("Code")),
                Name = reader.GetString(reader.GetOrdinal("Name")),
                IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive")),
                UpdatedAtUtc = reader.GetDateTime(reader.GetOrdinal("UpdatedAt"))
            });
        }

        return rows;
    }

    private async Task<List<AcademicTermSourceRow>> GetAcademicTermSourceRowsAsync(CancellationToken cancellationToken)
    {
        var rows = new List<AcademicTermSourceRow>();
        await using var connection = new NpgsqlConnection(_supabaseConnectionString);
        await connection.OpenAsync(cancellationToken);
        const string sql = @"
SELECT ""Id"", ""TermCode"", ""TermName"", ""SchoolYearCode"", ""StartDate"", ""EndDate"", ""IsInCurrentSchoolYear"", ""UpdatedAt""
FROM ""AcademicTerm""
ORDER BY ""Id"";";
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new AcademicTermSourceRow
            {
                Id = reader.GetInt32(reader.GetOrdinal("Id")),
                TermCode = reader.GetString(reader.GetOrdinal("TermCode")),
                TermName = reader.GetString(reader.GetOrdinal("TermName")),
                SchoolYearCode = reader.GetString(reader.GetOrdinal("SchoolYearCode")),
                StartDate = reader.GetDateTime(reader.GetOrdinal("StartDate")),
                EndDate = reader.GetDateTime(reader.GetOrdinal("EndDate")),
                IsInCurrentSchoolYear = reader.GetBoolean(reader.GetOrdinal("IsInCurrentSchoolYear")),
                UpdatedAtUtc = reader.GetDateTime(reader.GetOrdinal("UpdatedAt"))
            });
        }

        return rows;
    }

    private async Task<List<GradeSectionMentorSourceRow>> GetGradeSectionMentorSourceRowsAsync(CancellationToken cancellationToken)
    {
        var rows = new List<GradeSectionMentorSourceRow>();
        await using var connection = new NpgsqlConnection(_supabaseConnectionString);
        await connection.OpenAsync(cancellationToken);
        const string sql = @"
SELECT ""Id"", ""GradeName"", ""SectionName"", ""MentorStaffCode"", ""MentorFullName"", ""UpdatedAt""
FROM ""GradeSectionMentor""
ORDER BY ""Id"";";
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new GradeSectionMentorSourceRow
            {
                Id = reader.GetInt32(reader.GetOrdinal("Id")),
                GradeName = reader.GetString(reader.GetOrdinal("GradeName")),
                SectionName = reader.GetString(reader.GetOrdinal("SectionName")),
                MentorStaffCode = reader.GetString(reader.GetOrdinal("MentorStaffCode")),
                MentorFullName = reader.GetString(reader.GetOrdinal("MentorFullName")),
                UpdatedAtUtc = reader.GetDateTime(reader.GetOrdinal("UpdatedAt"))
            });
        }

        return rows;
    }

    private async Task<List<HrisLeaveBalanceSourceRow>> GetHrisLeaveBalanceSourceRowsAsync(CancellationToken cancellationToken)
    {
        var rows = new List<HrisLeaveBalanceSourceRow>();
        await using var connection = new NpgsqlConnection(_supabaseConnectionString);
        await connection.OpenAsync(cancellationToken);
        const string sql = @"
SELECT ""Id"", ""StaffCode"", ""LeaveType"", ""BalanceDays"", ""AsOfDate"", ""UpdatedAt""
FROM ""HrisLeaveBalance""
ORDER BY ""Id"";";
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new HrisLeaveBalanceSourceRow
            {
                Id = reader.GetInt32(reader.GetOrdinal("Id")),
                StaffCode = reader.GetString(reader.GetOrdinal("StaffCode")),
                LeaveType = reader.GetString(reader.GetOrdinal("LeaveType")),
                BalanceDays = reader.GetDecimal(reader.GetOrdinal("BalanceDays")),
                AsOfDate = reader.GetDateTime(reader.GetOrdinal("AsOfDate")),
                UpdatedAtUtc = reader.GetDateTime(reader.GetOrdinal("UpdatedAt"))
            });
        }

        return rows;
    }

    private static async Task UpsertAcademicProgramMirrorAsync(SqlConnection connection, AcademicProgramSourceRow row, CancellationToken cancellationToken)
    {
        const string sql = @"
MERGE dbo.AcademicProgramMirror AS target
USING (SELECT @Id AS Id, @Code AS Code, @Name AS Name, @IsActive AS IsActive, @UpdatedAt AS UpdatedAt) AS source
ON target.Id = source.Id
WHEN MATCHED AND target.UpdatedAt <= source.UpdatedAt THEN
    UPDATE SET Code = source.Code, Name = source.Name, IsActive = source.IsActive, UpdatedAt = source.UpdatedAt, LastSyncedAt = SYSUTCDATETIME()
WHEN NOT MATCHED THEN
    INSERT (Id, Code, Name, IsActive, UpdatedAt, LastSyncedAt)
    VALUES (source.Id, source.Code, source.Name, source.IsActive, source.UpdatedAt, SYSUTCDATETIME());";
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@Id", SqlDbType.Int).Value = row.Id;
        command.Parameters.Add("@Code", SqlDbType.NVarChar, 50).Value = row.Code;
        command.Parameters.Add("@Name", SqlDbType.NVarChar, 200).Value = row.Name;
        command.Parameters.Add("@IsActive", SqlDbType.Bit).Value = row.IsActive;
        command.Parameters.Add("@UpdatedAt", SqlDbType.DateTimeOffset).Value = new DateTimeOffset(DateTime.SpecifyKind(row.UpdatedAtUtc, DateTimeKind.Utc));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task UpsertAcademicTermMirrorAsync(SqlConnection connection, AcademicTermSourceRow row, CancellationToken cancellationToken)
    {
        const string sql = @"
MERGE dbo.AcademicTermMirror AS target
USING (
    SELECT @Id AS Id, @TermCode AS TermCode, @TermName AS TermName, @SchoolYearCode AS SchoolYearCode,
           @StartDate AS StartDate, @EndDate AS EndDate, @IsInCurrentSchoolYear AS IsInCurrentSchoolYear, @UpdatedAt AS UpdatedAt
) AS source
ON target.Id = source.Id
WHEN MATCHED AND target.UpdatedAt <= source.UpdatedAt THEN
    UPDATE SET TermCode = source.TermCode, TermName = source.TermName, SchoolYearCode = source.SchoolYearCode,
             StartDate = source.StartDate, EndDate = source.EndDate, IsInCurrentSchoolYear = source.IsInCurrentSchoolYear,
             UpdatedAt = source.UpdatedAt, LastSyncedAt = SYSUTCDATETIME()
WHEN NOT MATCHED THEN
    INSERT (Id, TermCode, TermName, SchoolYearCode, StartDate, EndDate, IsInCurrentSchoolYear, UpdatedAt, LastSyncedAt)
    VALUES (source.Id, source.TermCode, source.TermName, source.SchoolYearCode, source.StartDate, source.EndDate, source.IsInCurrentSchoolYear, source.UpdatedAt, SYSUTCDATETIME());";
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@Id", SqlDbType.Int).Value = row.Id;
        command.Parameters.Add("@TermCode", SqlDbType.NVarChar, 50).Value = row.TermCode;
        command.Parameters.Add("@TermName", SqlDbType.NVarChar, 200).Value = row.TermName;
        command.Parameters.Add("@SchoolYearCode", SqlDbType.NVarChar, 20).Value = row.SchoolYearCode;
        command.Parameters.Add("@StartDate", SqlDbType.Date).Value = row.StartDate.Date;
        command.Parameters.Add("@EndDate", SqlDbType.Date).Value = row.EndDate.Date;
        command.Parameters.Add("@IsInCurrentSchoolYear", SqlDbType.Bit).Value = row.IsInCurrentSchoolYear;
        command.Parameters.Add("@UpdatedAt", SqlDbType.DateTimeOffset).Value = new DateTimeOffset(DateTime.SpecifyKind(row.UpdatedAtUtc, DateTimeKind.Utc));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task UpsertGradeSectionMentorMirrorAsync(SqlConnection connection, GradeSectionMentorSourceRow row, CancellationToken cancellationToken)
    {
        const string sql = @"
MERGE dbo.GradeSectionMentorMirror AS target
USING (
    SELECT @Id AS Id, @GradeName AS GradeName, @SectionName AS SectionName, @MentorStaffCode AS MentorStaffCode,
           @MentorFullName AS MentorFullName, @UpdatedAt AS UpdatedAt
) AS source
ON target.Id = source.Id
WHEN MATCHED AND target.UpdatedAt <= source.UpdatedAt THEN
    UPDATE SET GradeName = source.GradeName, SectionName = source.SectionName, MentorStaffCode = source.MentorStaffCode,
             MentorFullName = source.MentorFullName, UpdatedAt = source.UpdatedAt, LastSyncedAt = SYSUTCDATETIME()
WHEN NOT MATCHED THEN
    INSERT (Id, GradeName, SectionName, MentorStaffCode, MentorFullName, UpdatedAt, LastSyncedAt)
    VALUES (source.Id, source.GradeName, source.SectionName, source.MentorStaffCode, source.MentorFullName, source.UpdatedAt, SYSUTCDATETIME());";
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@Id", SqlDbType.Int).Value = row.Id;
        command.Parameters.Add("@GradeName", SqlDbType.NVarChar, 80).Value = row.GradeName;
        command.Parameters.Add("@SectionName", SqlDbType.NVarChar, 80).Value = row.SectionName;
        command.Parameters.Add("@MentorStaffCode", SqlDbType.NVarChar, 50).Value = row.MentorStaffCode;
        command.Parameters.Add("@MentorFullName", SqlDbType.NVarChar, 200).Value = row.MentorFullName;
        command.Parameters.Add("@UpdatedAt", SqlDbType.DateTimeOffset).Value = new DateTimeOffset(DateTime.SpecifyKind(row.UpdatedAtUtc, DateTimeKind.Utc));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task UpsertHrisLeaveBalanceMirrorAsync(SqlConnection connection, HrisLeaveBalanceSourceRow row, CancellationToken cancellationToken)
    {
        const string sql = @"
MERGE dbo.HrisLeaveBalanceMirror AS target
USING (
    SELECT @Id AS Id, @StaffCode AS StaffCode, @LeaveType AS LeaveType, @BalanceDays AS BalanceDays, @AsOfDate AS AsOfDate, @UpdatedAt AS UpdatedAt
) AS source
ON target.Id = source.Id
WHEN MATCHED AND target.UpdatedAt <= source.UpdatedAt THEN
    UPDATE SET StaffCode = source.StaffCode, LeaveType = source.LeaveType, BalanceDays = source.BalanceDays,
             AsOfDate = source.AsOfDate, UpdatedAt = source.UpdatedAt, LastSyncedAt = SYSUTCDATETIME()
WHEN NOT MATCHED THEN
    INSERT (Id, StaffCode, LeaveType, BalanceDays, AsOfDate, UpdatedAt, LastSyncedAt)
    VALUES (source.Id, source.StaffCode, source.LeaveType, source.BalanceDays, source.AsOfDate, source.UpdatedAt, SYSUTCDATETIME());";
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@Id", SqlDbType.Int).Value = row.Id;
        command.Parameters.Add("@StaffCode", SqlDbType.NVarChar, 50).Value = row.StaffCode;
        command.Parameters.Add("@LeaveType", SqlDbType.NVarChar, 80).Value = row.LeaveType;
        var balance = command.Parameters.Add("@BalanceDays", SqlDbType.Decimal);
        balance.Precision = 6;
        balance.Scale = 2;
        balance.Value = row.BalanceDays;
        command.Parameters.Add("@AsOfDate", SqlDbType.Date).Value = row.AsOfDate.Date;
        command.Parameters.Add("@UpdatedAt", SqlDbType.DateTimeOffset).Value = new DateTimeOffset(DateTime.SpecifyKind(row.UpdatedAtUtc, DateTimeKind.Utc));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<HashSet<int>> GetAcademicProgramSourceIdsAsync(CancellationToken cancellationToken) =>
        await ReadIntIdSetAsync("SELECT \"Id\" FROM \"AcademicProgram\"", cancellationToken);

    private async Task<HashSet<int>> GetAcademicProgramTargetIdsAsync(CancellationToken cancellationToken) =>
        await ReadMssqlIntIdSetAsync("SELECT Id FROM dbo.AcademicProgramMirror", cancellationToken);

    private async Task<HashSet<int>> GetAcademicTermSourceIdsAsync(CancellationToken cancellationToken) =>
        await ReadIntIdSetAsync("SELECT \"Id\" FROM \"AcademicTerm\"", cancellationToken);

    private async Task<HashSet<int>> GetAcademicTermTargetIdsAsync(CancellationToken cancellationToken) =>
        await ReadMssqlIntIdSetAsync("SELECT Id FROM dbo.AcademicTermMirror", cancellationToken);

    private async Task<HashSet<int>> GetGradeSectionMentorSourceIdsAsync(CancellationToken cancellationToken) =>
        await ReadIntIdSetAsync("SELECT \"Id\" FROM \"GradeSectionMentor\"", cancellationToken);

    private async Task<HashSet<int>> GetGradeSectionMentorTargetIdsAsync(CancellationToken cancellationToken) =>
        await ReadMssqlIntIdSetAsync("SELECT Id FROM dbo.GradeSectionMentorMirror", cancellationToken);

    private async Task<HashSet<int>> GetHrisLeaveBalanceSourceIdsAsync(CancellationToken cancellationToken) =>
        await ReadIntIdSetAsync("SELECT \"Id\" FROM \"HrisLeaveBalance\"", cancellationToken);

    private async Task<HashSet<int>> GetHrisLeaveBalanceTargetIdsAsync(CancellationToken cancellationToken) =>
        await ReadMssqlIntIdSetAsync("SELECT Id FROM dbo.HrisLeaveBalanceMirror", cancellationToken);

    private async Task<HashSet<int>> ReadIntIdSetAsync(string sql, CancellationToken cancellationToken)
    {
        var ids = new HashSet<int>();
        await using var connection = new NpgsqlConnection(_supabaseConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            ids.Add(reader.GetInt32(0));
        }

        return ids;
    }

    private async Task<HashSet<int>> ReadMssqlIntIdSetAsync(string sql, CancellationToken cancellationToken)
    {
        var ids = new HashSet<int>();
        await using var connection = new SqlConnection(_mssqlConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            ids.Add(reader.GetInt32(0));
        }

        return ids;
    }

    private sealed class AcademicProgramSourceRow
    {
        public int Id { get; init; }
        public string Code { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public bool IsActive { get; init; }
        public DateTime UpdatedAtUtc { get; init; }
    }

    private sealed class AcademicTermSourceRow
    {
        public int Id { get; init; }
        public string TermCode { get; init; } = string.Empty;
        public string TermName { get; init; } = string.Empty;
        public string SchoolYearCode { get; init; } = string.Empty;
        public DateTime StartDate { get; init; }
        public DateTime EndDate { get; init; }
        public bool IsInCurrentSchoolYear { get; init; }
        public DateTime UpdatedAtUtc { get; init; }
    }

    private sealed class GradeSectionMentorSourceRow
    {
        public int Id { get; init; }
        public string GradeName { get; init; } = string.Empty;
        public string SectionName { get; init; } = string.Empty;
        public string MentorStaffCode { get; init; } = string.Empty;
        public string MentorFullName { get; init; } = string.Empty;
        public DateTime UpdatedAtUtc { get; init; }
    }

    private sealed class HrisLeaveBalanceSourceRow
    {
        public int Id { get; init; }
        public string StaffCode { get; init; } = string.Empty;
        public string LeaveType { get; init; } = string.Empty;
        public decimal BalanceDays { get; init; }
        public DateTime AsOfDate { get; init; }
        public DateTime UpdatedAtUtc { get; init; }
    }
}
