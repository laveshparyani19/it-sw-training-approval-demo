using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace ApprovalDemo.Api.Services
{
    public sealed partial class ApprovalSyncService
    {
        private const string SyncName = "ApprovalToMssql";
        private readonly string _supabaseConnectionString;
        private readonly string? _mssqlConnectionString;
        private readonly bool _syncExplicitlyEnabled;
        private readonly ILogger<ApprovalSyncService> _logger;
        private readonly SemaphoreSlim _syncLock = new(1, 1);
        private DateTime _lastReconciliationDateUtc = DateTime.MinValue;
        private volatile bool _mssqlReady;
        private string _mssqlDisabledReason = "MSSQL sync not initialized.";

        public ApprovalSyncService(IConfiguration configuration, ILogger<ApprovalSyncService> logger)
        {
            _logger = logger;
            _supabaseConnectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
            _mssqlConnectionString = configuration.GetConnectionString("MssqlReporting");
            _syncExplicitlyEnabled = !string.Equals(
                Environment.GetEnvironmentVariable("ENABLE_MSSQL_SYNC") ?? configuration["ENABLE_MSSQL_SYNC"],
                "false",
                StringComparison.OrdinalIgnoreCase);

            _mssqlReady = _syncExplicitlyEnabled && !string.IsNullOrWhiteSpace(_mssqlConnectionString);
            if (!_syncExplicitlyEnabled)
            {
                _mssqlDisabledReason = "MSSQL sync disabled. Set ENABLE_MSSQL_SYNC=true or remove ENABLE_MSSQL_SYNC=false.";
            }
            else if (!_mssqlReady)
            {
                _mssqlDisabledReason = "MSSQL_CONNECTION_STRING is not configured.";
            }
        }

        public bool IsEnabled => _syncExplicitlyEnabled && !string.IsNullOrWhiteSpace(_mssqlConnectionString);

        public async Task InitializeAsync(CancellationToken cancellationToken)
        {
            await EnsureSupabaseSchemaAsync(cancellationToken);
            if (IsEnabled)
            {
                try
                {
                    await EnsureMssqlSchemaAsync(cancellationToken);
                    await SyncStudentDirectorySnapshotAsync(cancellationToken);
                    await SyncStaffDirectorySnapshotAsync(cancellationToken);
                    await SyncTlTeamAssignmentSnapshotAsync(cancellationToken);
                    await SyncTask8DataSnapshotAsync(cancellationToken);
                    await SynchronizeMirrorDeletesAsync(cancellationToken);
                    _mssqlReady = true;
                    _mssqlDisabledReason = string.Empty;
                }
                catch (Exception ex)
                {
                    _mssqlReady = false;
                    _mssqlDisabledReason = $"MSSQL target unavailable: {ex.Message}";
                    _logger.LogWarning(ex, "MSSQL sync initialization skipped. API will continue using Supabase as primary.");
                }
            }
        }

        public async Task<SyncRunResult> RunOnceAsync(CancellationToken cancellationToken)
        {
            await _syncLock.WaitAsync(cancellationToken);
            try
            {
                await EnsureSupabaseSchemaAsync(cancellationToken);

                if (!IsEnabled)
                {
                    return SyncRunResult.Disabled("MSSQL_CONNECTION_STRING is not configured.");
                }

                if (!_mssqlReady)
                {
                    try
                    {
                        await EnsureMssqlSchemaAsync(cancellationToken);
                        _mssqlReady = true;
                        _mssqlDisabledReason = string.Empty;
                    }
                    catch (Exception ex)
                    {
                        _mssqlReady = false;
                        _mssqlDisabledReason = $"MSSQL target unavailable: {ex.Message}";
                        _logger.LogWarning(ex, "Sync run skipped because MSSQL target is unreachable.");
                        return SyncRunResult.Disabled(_mssqlDisabledReason);
                    }
                }

                var currentWatermark = await GetWatermarkAsync(cancellationToken);
                var changedRows = await GetChangedRowsAsync(currentWatermark, batchSize: 200, cancellationToken);

                if (changedRows.Count == 0)
                {
                    var studentUpsertsOnNoDelta = await SyncStudentDirectorySnapshotAsync(cancellationToken);
                    var staffUpsertsOnNoDelta = await SyncStaffDirectorySnapshotAsync(cancellationToken);
                    var tlUpsertsOnNoDelta = await SyncTlTeamAssignmentSnapshotAsync(cancellationToken);
                    var task8UpsertsOnNoDelta = await SyncTask8DataSnapshotAsync(cancellationToken);
                    var noDeltaDeletes = await SynchronizeMirrorDeletesAsync(cancellationToken);
                    await MaybeRunDailyReconciliationAsync(cancellationToken);
                    return new SyncRunResult
                    {
                        Enabled = true,
                        WatermarkFromUtc = currentWatermark,
                        WatermarkToUtc = currentWatermark,
                        Processed = 0,
                        Successful = 0,
                        Failed = 0,
                        Message = $"No changed approval rows found. StudentDirectory snapshot upserted {studentUpsertsOnNoDelta} rows, StaffDirectory snapshot upserted {staffUpsertsOnNoDelta} rows, TlTeamAssignment snapshot upserted {tlUpsertsOnNoDelta} rows, Task8 reference data upserted {task8UpsertsOnNoDelta} rows. Mirror cleanup removed {noDeltaDeletes.approvalDeleted} approval rows, {noDeltaDeletes.studentDeleted} student rows, {noDeltaDeletes.staffDeleted} staff rows, {noDeltaDeletes.tlDeleted} TL assignment rows and {noDeltaDeletes.task8Deleted} Task8 mirror rows."
                    };
                }

                DateTime latestSuccessfulWatermark = currentWatermark;
                int successful = 0;
                int failed = 0;

                foreach (var row in changedRows)
                {
                    var upserted = await TryUpsertWithRetriesAsync(row, cancellationToken);
                    if (!upserted)
                    {
                        failed++;
                        break;
                    }

                    successful++;
                    if (row.UpdatedAtUtc > latestSuccessfulWatermark)
                    {
                        latestSuccessfulWatermark = row.UpdatedAtUtc;
                    }
                }

                if (latestSuccessfulWatermark > currentWatermark)
                {
                    await SetWatermarkAsync(latestSuccessfulWatermark, cancellationToken);
                }

                var studentUpserts = await SyncStudentDirectorySnapshotAsync(cancellationToken);
                var staffUpserts = await SyncStaffDirectorySnapshotAsync(cancellationToken);
                var tlUpserts = await SyncTlTeamAssignmentSnapshotAsync(cancellationToken);
                var task8Upserts = await SyncTask8DataSnapshotAsync(cancellationToken);
                var deleteSync = await SynchronizeMirrorDeletesAsync(cancellationToken);
                await MaybeRunDailyReconciliationAsync(cancellationToken);

                return new SyncRunResult
                {
                    Enabled = true,
                    WatermarkFromUtc = currentWatermark,
                    WatermarkToUtc = latestSuccessfulWatermark,
                    Processed = successful + failed,
                    Successful = successful,
                    Failed = failed,
                    Message = failed > 0
                        ? "Stopped at first failed row. Failed row logged to dead-letter table for review."
                        : $"Sync completed successfully. StudentDirectory snapshot upserted {studentUpserts} rows, StaffDirectory snapshot upserted {staffUpserts} rows, TlTeamAssignment snapshot upserted {tlUpserts} rows, Task8 reference data upserted {task8Upserts} rows. Mirror cleanup removed {deleteSync.approvalDeleted} approval rows, {deleteSync.studentDeleted} student rows, {deleteSync.staffDeleted} staff rows, {deleteSync.tlDeleted} TL assignment rows and {deleteSync.task8Deleted} Task8 mirror rows."
                };
            }
            finally
            {
                _syncLock.Release();
            }
        }

        public async Task<SyncReconciliationResult> RunReconciliationNowAsync(CancellationToken cancellationToken)
        {
            await _syncLock.WaitAsync(cancellationToken);
            try
            {
                await EnsureSupabaseSchemaAsync(cancellationToken);

                if (!IsEnabled)
                {
                    return new SyncReconciliationResult
                    {
                        Enabled = false,
                        GeneratedAtUtc = DateTime.UtcNow,
                        Message = "MSSQL_CONNECTION_STRING is not configured."
                    };
                }

                if (!_mssqlReady)
                {
                    try
                    {
                        await EnsureMssqlSchemaAsync(cancellationToken);
                        _mssqlReady = true;
                        _mssqlDisabledReason = string.Empty;
                    }
                    catch (Exception ex)
                    {
                        _mssqlReady = false;
                        _mssqlDisabledReason = $"MSSQL target unavailable: {ex.Message}";
                        _logger.LogWarning(ex, "Reconciliation skipped because MSSQL target is unreachable.");
                        return new SyncReconciliationResult
                        {
                            Enabled = false,
                            GeneratedAtUtc = DateTime.UtcNow,
                            Message = _mssqlDisabledReason
                        };
                    }
                }
                var report = await GenerateReconciliationReportAsync(cancellationToken);
                await SaveReconciliationReportAsync(report, cancellationToken);
                _lastReconciliationDateUtc = DateTime.UtcNow.Date;
                return report;
            }
            finally
            {
                _syncLock.Release();
            }
        }

        private async Task<bool> TryUpsertWithRetriesAsync(SyncSourceRow row, CancellationToken cancellationToken)
        {
            const int maxAttempts = 3;
            Exception? lastException = null;

            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    await UpsertRowToMssqlAsync(row, cancellationToken);
                    return true;
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    _logger.LogWarning(ex, "Attempt {Attempt} failed while syncing row {RowId} (OperationId: {OperationId}).", attempt, row.Id, row.OperationId);

                    if (attempt < maxAttempts)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(attempt * 2), cancellationToken);
                    }
                }
            }

            if (lastException is not null)
            {
                await LogDeadLetterAsync(row, maxAttempts, lastException, cancellationToken);
                _logger.LogError(lastException, "Row {RowId} moved to dead-letter after retries.", row.Id);
            }

            return false;
        }

        private async Task<int> SyncStudentDirectorySnapshotAsync(CancellationToken cancellationToken)
        {
            var rows = await GetStudentSourceRowsAsync(cancellationToken);
            if (rows.Count == 0)
            {
                return 0;
            }

            await using var connection = new SqlConnection(_mssqlConnectionString);
            await connection.OpenAsync(cancellationToken);

            foreach (var row in rows)
            {
                await UpsertStudentRowToMssqlAsync(connection, row, cancellationToken);
            }

            return rows.Count;
        }

        private async Task<List<StudentSourceRow>> GetStudentSourceRowsAsync(CancellationToken cancellationToken)
        {
            var rows = new List<StudentSourceRow>();

            await using var connection = new NpgsqlConnection(_supabaseConnectionString);
            await connection.OpenAsync(cancellationToken);

            const string sql = @"
SELECT
    ""Id"",
    ""StudentCode"",
    ""FullName"",
    ""GradeName"",
    ""SectionName"",
    ""PhotoUrl"",
    COALESCE(""IsActive"", TRUE) AS ""IsActive"",
    ""ProgramCode"",
    COALESCE(NULLIF(TRIM(""AcademicYearCode""), ''), '2025-26') AS ""AcademicYearCode"",
    ""UpdatedAt""
FROM ""StudentDirectory""
ORDER BY ""Id"";";

            await using var command = new NpgsqlCommand(sql, connection);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                var programOrdinal = reader.GetOrdinal("ProgramCode");
                rows.Add(new StudentSourceRow
                {
                    Id = reader.GetInt32(reader.GetOrdinal("Id")),
                    StudentCode = reader.GetString(reader.GetOrdinal("StudentCode")),
                    FullName = reader.GetString(reader.GetOrdinal("FullName")),
                    GradeName = reader.GetString(reader.GetOrdinal("GradeName")),
                    SectionName = reader.GetString(reader.GetOrdinal("SectionName")),
                    PhotoUrl = reader.IsDBNull(reader.GetOrdinal("PhotoUrl")) ? null : reader.GetString(reader.GetOrdinal("PhotoUrl")),
                    IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive")),
                    ProgramCode = reader.IsDBNull(programOrdinal) ? null : reader.GetString(programOrdinal),
                    AcademicYearCode = reader.GetString(reader.GetOrdinal("AcademicYearCode")),
                    UpdatedAtUtc = reader.GetDateTime(reader.GetOrdinal("UpdatedAt"))
                });
            }

            return rows;
        }

        private static async Task UpsertStudentRowToMssqlAsync(SqlConnection connection, StudentSourceRow row, CancellationToken cancellationToken)
        {
            const string sql = @"
MERGE dbo.StudentDirectoryMirror AS target
USING (
    SELECT
        @Id AS Id,
        @StudentCode AS StudentCode,
        @FullName AS FullName,
        @GradeName AS GradeName,
        @SectionName AS SectionName,
        @PhotoUrl AS PhotoUrl,
        @IsActive AS IsActive,
        @ProgramCode AS ProgramCode,
        @AcademicYearCode AS AcademicYearCode,
        @UpdatedAt AS UpdatedAt
) AS source
ON target.Id = source.Id
WHEN MATCHED AND target.UpdatedAt <= source.UpdatedAt THEN
    UPDATE SET
        StudentCode = source.StudentCode,
        FullName = source.FullName,
        GradeName = source.GradeName,
        SectionName = source.SectionName,
        PhotoUrl = source.PhotoUrl,
        IsActive = source.IsActive,
        ProgramCode = source.ProgramCode,
        AcademicYearCode = source.AcademicYearCode,
        UpdatedAt = source.UpdatedAt,
        LastSyncedAt = SYSUTCDATETIME()
WHEN NOT MATCHED THEN
    INSERT (Id, StudentCode, FullName, GradeName, SectionName, PhotoUrl, IsActive, ProgramCode, AcademicYearCode, UpdatedAt, LastSyncedAt)
    VALUES (source.Id, source.StudentCode, source.FullName, source.GradeName, source.SectionName, source.PhotoUrl, source.IsActive, source.ProgramCode, source.AcademicYearCode, source.UpdatedAt, SYSUTCDATETIME());";

            await using var command = new SqlCommand(sql, connection);
            command.Parameters.Add("@Id", SqlDbType.Int).Value = row.Id;
            command.Parameters.Add("@StudentCode", SqlDbType.NVarChar, 50).Value = row.StudentCode;
            command.Parameters.Add("@FullName", SqlDbType.NVarChar, 200).Value = row.FullName;
            command.Parameters.Add("@GradeName", SqlDbType.NVarChar, 50).Value = row.GradeName;
            command.Parameters.Add("@SectionName", SqlDbType.NVarChar, 50).Value = row.SectionName;
            command.Parameters.Add("@PhotoUrl", SqlDbType.NVarChar, -1).Value = (object?)row.PhotoUrl ?? DBNull.Value;
            command.Parameters.Add("@IsActive", SqlDbType.Bit).Value = row.IsActive;
            command.Parameters.Add("@ProgramCode", SqlDbType.NVarChar, 50).Value = (object?)row.ProgramCode ?? DBNull.Value;
            command.Parameters.Add("@AcademicYearCode", SqlDbType.NVarChar, 20).Value = row.AcademicYearCode;
            command.Parameters.Add("@UpdatedAt", SqlDbType.DateTimeOffset).Value = new DateTimeOffset(DateTime.SpecifyKind(row.UpdatedAtUtc, DateTimeKind.Utc));

            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        private async Task<int> SyncStaffDirectorySnapshotAsync(CancellationToken cancellationToken)
        {
            var rows = await GetStaffSourceRowsAsync(cancellationToken);
            if (rows.Count == 0)
            {
                return 0;
            }

            await using var connection = new SqlConnection(_mssqlConnectionString);
            await connection.OpenAsync(cancellationToken);

            foreach (var row in rows)
            {
                await UpsertStaffRowToMssqlAsync(connection, row, cancellationToken);
            }

            return rows.Count;
        }

        private async Task<List<StaffSourceRow>> GetStaffSourceRowsAsync(CancellationToken cancellationToken)
        {
            var rows = new List<StaffSourceRow>();

            await using var connection = new NpgsqlConnection(_supabaseConnectionString);
            await connection.OpenAsync(cancellationToken);

            const string sql = @"
SELECT
    ""Id"",
    ""StaffCode"",
    ""FullName"",
    ""DepartmentName"",
    ""TeamName"",
    ""Designation"",
    ""PhotoUrl"",
    COALESCE(""IsActive"", TRUE) AS ""IsActive"",
    COALESCE(""IsSystemAccount"", FALSE) AS ""IsSystemAccount"",
    COALESCE(NULLIF(TRIM(""StaffCategory""), ''), 'Academic') AS ""StaffCategory"",
    ""UpdatedAt""
FROM ""StaffDirectory""
ORDER BY ""Id"";";

            await using var command = new NpgsqlCommand(sql, connection);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                rows.Add(new StaffSourceRow
                {
                    Id = reader.GetInt32(reader.GetOrdinal("Id")),
                    StaffCode = reader.GetString(reader.GetOrdinal("StaffCode")),
                    FullName = reader.GetString(reader.GetOrdinal("FullName")),
                    DepartmentName = reader.GetString(reader.GetOrdinal("DepartmentName")),
                    TeamName = reader.GetString(reader.GetOrdinal("TeamName")),
                    Designation = reader.GetString(reader.GetOrdinal("Designation")),
                    PhotoUrl = reader.IsDBNull(reader.GetOrdinal("PhotoUrl")) ? null : reader.GetString(reader.GetOrdinal("PhotoUrl")),
                    IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive")),
                    IsSystemAccount = reader.GetBoolean(reader.GetOrdinal("IsSystemAccount")),
                    StaffCategory = reader.GetString(reader.GetOrdinal("StaffCategory")),
                    UpdatedAtUtc = reader.GetDateTime(reader.GetOrdinal("UpdatedAt"))
                });
            }

            return rows;
        }

        private static async Task UpsertStaffRowToMssqlAsync(SqlConnection connection, StaffSourceRow row, CancellationToken cancellationToken)
        {
            const string sql = @"
MERGE dbo.StaffDirectoryMirror AS target
USING (
    SELECT
        @Id AS Id,
        @StaffCode AS StaffCode,
        @FullName AS FullName,
        @DepartmentName AS DepartmentName,
        @TeamName AS TeamName,
        @Designation AS Designation,
        @PhotoUrl AS PhotoUrl,
        @IsActive AS IsActive,
        @IsSystemAccount AS IsSystemAccount,
        @StaffCategory AS StaffCategory,
        @UpdatedAt AS UpdatedAt
) AS source
ON target.Id = source.Id
WHEN MATCHED AND target.UpdatedAt <= source.UpdatedAt THEN
    UPDATE SET
        StaffCode = source.StaffCode,
        FullName = source.FullName,
        DepartmentName = source.DepartmentName,
        TeamName = source.TeamName,
        Designation = source.Designation,
        PhotoUrl = source.PhotoUrl,
        IsActive = source.IsActive,
        IsSystemAccount = source.IsSystemAccount,
        StaffCategory = source.StaffCategory,
        UpdatedAt = source.UpdatedAt,
        LastSyncedAt = SYSUTCDATETIME()
WHEN NOT MATCHED THEN
    INSERT (Id, StaffCode, FullName, DepartmentName, TeamName, Designation, PhotoUrl, IsActive, IsSystemAccount, StaffCategory, UpdatedAt, LastSyncedAt)
    VALUES (source.Id, source.StaffCode, source.FullName, source.DepartmentName, source.TeamName, source.Designation, source.PhotoUrl, source.IsActive, source.IsSystemAccount, source.StaffCategory, source.UpdatedAt, SYSUTCDATETIME());";

            await using var command = new SqlCommand(sql, connection);
            command.Parameters.Add("@Id", SqlDbType.Int).Value = row.Id;
            command.Parameters.Add("@StaffCode", SqlDbType.NVarChar, 50).Value = row.StaffCode;
            command.Parameters.Add("@FullName", SqlDbType.NVarChar, 200).Value = row.FullName;
            command.Parameters.Add("@DepartmentName", SqlDbType.NVarChar, 100).Value = row.DepartmentName;
            command.Parameters.Add("@TeamName", SqlDbType.NVarChar, 100).Value = row.TeamName;
            command.Parameters.Add("@Designation", SqlDbType.NVarChar, 120).Value = row.Designation;
            command.Parameters.Add("@PhotoUrl", SqlDbType.NVarChar, -1).Value = (object?)row.PhotoUrl ?? DBNull.Value;
            command.Parameters.Add("@IsActive", SqlDbType.Bit).Value = row.IsActive;
            command.Parameters.Add("@IsSystemAccount", SqlDbType.Bit).Value = row.IsSystemAccount;
            command.Parameters.Add("@StaffCategory", SqlDbType.NVarChar, 30).Value = row.StaffCategory;
            command.Parameters.Add("@UpdatedAt", SqlDbType.DateTimeOffset).Value = new DateTimeOffset(DateTime.SpecifyKind(row.UpdatedAtUtc, DateTimeKind.Utc));

            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        private async Task<int> SyncTlTeamAssignmentSnapshotAsync(CancellationToken cancellationToken)
        {
            var rows = await GetTlAssignmentSourceRowsAsync(cancellationToken);
            if (rows.Count == 0)
            {
                return 0;
            }

            await using var connection = new SqlConnection(_mssqlConnectionString);
            await connection.OpenAsync(cancellationToken);

            foreach (var row in rows)
            {
                await UpsertTlAssignmentRowToMssqlAsync(connection, row, cancellationToken);
            }

            return rows.Count;
        }

        private async Task<List<TlAssignmentSourceRow>> GetTlAssignmentSourceRowsAsync(CancellationToken cancellationToken)
        {
            var rows = new List<TlAssignmentSourceRow>();

            await using var connection = new NpgsqlConnection(_supabaseConnectionString);
            await connection.OpenAsync(cancellationToken);

            const string sql = @"
SELECT
    ""Id"",
    ""TlStaffCode"",
    ""DepartmentName"",
    ""TeamName"",
    ""MemberStaffIds"",
    ""TaskDescription"",
    ""CreatedAt"",
    ""UpdatedAt""
FROM ""TlTeamAssignment""
ORDER BY ""CreatedAt"";";

            await using var command = new NpgsqlCommand(sql, connection);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                var idsOrdinal = reader.GetOrdinal("MemberStaffIds");
                var taskOrdinal = reader.GetOrdinal("TaskDescription");
                int[] memberIds = reader.IsDBNull(idsOrdinal)
                    ? Array.Empty<int>()
                    : (int[])reader.GetValue(idsOrdinal);

                rows.Add(new TlAssignmentSourceRow
                {
                    Id = reader.GetGuid(reader.GetOrdinal("Id")),
                    TlStaffCode = reader.GetString(reader.GetOrdinal("TlStaffCode")),
                    DepartmentName = reader.GetString(reader.GetOrdinal("DepartmentName")),
                    TeamName = reader.GetString(reader.GetOrdinal("TeamName")),
                    MemberStaffIds = memberIds,
                    TaskDescription = reader.IsDBNull(taskOrdinal) ? null : reader.GetString(taskOrdinal),
                    CreatedAtUtc = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
                    UpdatedAtUtc = reader.GetDateTime(reader.GetOrdinal("UpdatedAt"))
                });
            }

            return rows;
        }

        private static async Task UpsertTlAssignmentRowToMssqlAsync(SqlConnection connection, TlAssignmentSourceRow row, CancellationToken cancellationToken)
        {
            const string sql = @"
MERGE dbo.TlTeamAssignmentMirror AS target
USING (
    SELECT
        @Id AS Id,
        @TlStaffCode AS TlStaffCode,
        @DepartmentName AS DepartmentName,
        @TeamName AS TeamName,
        @MemberStaffIdsJson AS MemberStaffIdsJson,
        @TaskDescription AS TaskDescription,
        @CreatedAt AS CreatedAt,
        @UpdatedAt AS UpdatedAt
) AS source
ON target.Id = source.Id
WHEN MATCHED AND target.UpdatedAt <= source.UpdatedAt THEN
    UPDATE SET
        TlStaffCode = source.TlStaffCode,
        DepartmentName = source.DepartmentName,
        TeamName = source.TeamName,
        MemberStaffIdsJson = source.MemberStaffIdsJson,
        TaskDescription = source.TaskDescription,
        CreatedAt = source.CreatedAt,
        UpdatedAt = source.UpdatedAt,
        LastSyncedAt = SYSUTCDATETIME()
WHEN NOT MATCHED THEN
    INSERT (Id, TlStaffCode, DepartmentName, TeamName, MemberStaffIdsJson, TaskDescription, CreatedAt, UpdatedAt, LastSyncedAt)
    VALUES (source.Id, source.TlStaffCode, source.DepartmentName, source.TeamName, source.MemberStaffIdsJson, source.TaskDescription, source.CreatedAt, source.UpdatedAt, SYSUTCDATETIME());";

            await using var command = new SqlCommand(sql, connection);
            command.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = row.Id;
            command.Parameters.Add("@TlStaffCode", SqlDbType.NVarChar, 50).Value = row.TlStaffCode;
            command.Parameters.Add("@DepartmentName", SqlDbType.NVarChar, 100).Value = row.DepartmentName;
            command.Parameters.Add("@TeamName", SqlDbType.NVarChar, 100).Value = row.TeamName;
            command.Parameters.Add("@MemberStaffIdsJson", SqlDbType.NVarChar, -1).Value = JsonSerializer.Serialize(row.MemberStaffIds);
            command.Parameters.Add("@TaskDescription", SqlDbType.NVarChar, -1).Value = (object?)row.TaskDescription ?? DBNull.Value;
            command.Parameters.Add("@CreatedAt", SqlDbType.DateTimeOffset).Value = new DateTimeOffset(DateTime.SpecifyKind(row.CreatedAtUtc, DateTimeKind.Utc));
            command.Parameters.Add("@UpdatedAt", SqlDbType.DateTimeOffset).Value = new DateTimeOffset(DateTime.SpecifyKind(row.UpdatedAtUtc, DateTimeKind.Utc));

            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        private async Task<(int approvalDeleted, int studentDeleted, int staffDeleted, int tlDeleted, int task8Deleted)> SynchronizeMirrorDeletesAsync(CancellationToken cancellationToken)
        {
            var approvalSourceIds = await GetSourceIdsAsync(cancellationToken);
            var approvalTargetIds = await GetTargetIdsAsync(cancellationToken);
            var approvalStaleIds = approvalTargetIds.Where(id => !approvalSourceIds.Contains(id)).ToArray();

            var studentSourceIds = await GetStudentSourceIdsAsync(cancellationToken);
            var studentTargetIds = await GetStudentTargetIdsAsync(cancellationToken);
            var studentStaleIds = studentTargetIds.Where(id => !studentSourceIds.Contains(id)).ToArray();

            var staffSourceIds = await GetStaffSourceIdsAsync(cancellationToken);
            var staffTargetIds = await GetStaffTargetIdsAsync(cancellationToken);
            var staffStaleIds = staffTargetIds.Where(id => !staffSourceIds.Contains(id)).ToArray();

            var tlSourceIds = await GetTlAssignmentSourceIdsAsync(cancellationToken);
            var tlTargetIds = await GetTlAssignmentTargetIdsAsync(cancellationToken);
            var tlStaleIds = tlTargetIds.Where(id => !tlSourceIds.Contains(id)).ToArray();

            await using var connection = new SqlConnection(_mssqlConnectionString);
            await connection.OpenAsync(cancellationToken);

            var approvalDeleted = await DeleteRowsByIdsAsync(connection, "DELETE FROM dbo.ApprovalRequestMirror WHERE Id = @Id", approvalStaleIds, cancellationToken);
            var studentDeleted = await DeleteRowsByIdsAsync(connection, "DELETE FROM dbo.StudentDirectoryMirror WHERE Id = @Id", studentStaleIds, cancellationToken);
            var staffDeleted = await DeleteRowsByIdsAsync(connection, "DELETE FROM dbo.StaffDirectoryMirror WHERE Id = @Id", staffStaleIds, cancellationToken);
            var tlDeleted = await DeleteRowsByGuidsAsync(connection, "DELETE FROM dbo.TlTeamAssignmentMirror WHERE Id = @Id", tlStaleIds, cancellationToken);
            var task8Deleted = await SynchronizeTask8MirrorDeletesAsync(connection, cancellationToken);

            return (approvalDeleted, studentDeleted, staffDeleted, tlDeleted, task8Deleted);
        }

        private static async Task<int> DeleteRowsByGuidsAsync(SqlConnection connection, string sql, IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken)
        {
            if (ids.Count == 0)
            {
                return 0;
            }

            var deleted = 0;
            await using var command = new SqlCommand(sql, connection);
            var parameter = command.Parameters.Add("@Id", SqlDbType.UniqueIdentifier);

            foreach (var id in ids)
            {
                parameter.Value = id;
                deleted += await command.ExecuteNonQueryAsync(cancellationToken);
            }

            return deleted;
        }

        private static async Task<int> DeleteRowsByIdsAsync(SqlConnection connection, string sql, IReadOnlyCollection<int> ids, CancellationToken cancellationToken)
        {
            if (ids.Count == 0)
            {
                return 0;
            }

            var deleted = 0;
            await using var command = new SqlCommand(sql, connection);
            var parameter = command.Parameters.Add("@Id", SqlDbType.Int);

            foreach (var id in ids)
            {
                parameter.Value = id;
                deleted += await command.ExecuteNonQueryAsync(cancellationToken);
            }

            return deleted;
        }

        private async Task UpsertRowToMssqlAsync(SyncSourceRow row, CancellationToken cancellationToken)
        {
            await using var connection = new SqlConnection(_mssqlConnectionString);
            await connection.OpenAsync(cancellationToken);

            const string sql = @"
MERGE dbo.ApprovalRequestMirror AS target
USING (
    SELECT
        @Id AS Id,
        @Title AS Title,
        @RequestedBy AS RequestedBy,
        @Status AS Status,
        @CreatedAt AS CreatedAt,
        @UpdatedAt AS UpdatedAt,
        @IsDeleted AS IsDeleted,
        @DecisionBy AS DecisionBy,
        @DecisionAt AS DecisionAt,
        @RejectReason AS RejectReason,
        @OperationId AS OperationId
) AS source
ON target.Id = source.Id
WHEN MATCHED AND target.UpdatedAt <= source.UpdatedAt THEN
    UPDATE SET
        Title = source.Title,
        RequestedBy = source.RequestedBy,
        Status = source.Status,
        CreatedAt = source.CreatedAt,
        UpdatedAt = source.UpdatedAt,
        IsDeleted = source.IsDeleted,
        DecisionBy = source.DecisionBy,
        DecisionAt = source.DecisionAt,
        RejectReason = source.RejectReason,
        OperationId = source.OperationId,
        LastSyncedAt = SYSUTCDATETIME()
WHEN NOT MATCHED THEN
    INSERT (Id, Title, RequestedBy, Status, CreatedAt, UpdatedAt, IsDeleted, DecisionBy, DecisionAt, RejectReason, OperationId, LastSyncedAt)
    VALUES (source.Id, source.Title, source.RequestedBy, source.Status, source.CreatedAt, source.UpdatedAt, source.IsDeleted, source.DecisionBy, source.DecisionAt, source.RejectReason, source.OperationId, SYSUTCDATETIME());";

            await using var command = new SqlCommand(sql, connection);
            command.Parameters.Add("@Id", SqlDbType.Int).Value = row.Id;
            command.Parameters.Add("@Title", SqlDbType.NVarChar, 500).Value = row.Title;
            command.Parameters.Add("@RequestedBy", SqlDbType.NVarChar, 200).Value = row.RequestedBy;
            command.Parameters.Add("@Status", SqlDbType.TinyInt).Value = row.Status;
            command.Parameters.Add("@CreatedAt", SqlDbType.DateTimeOffset).Value = new DateTimeOffset(DateTime.SpecifyKind(row.CreatedAtUtc, DateTimeKind.Utc));
            command.Parameters.Add("@UpdatedAt", SqlDbType.DateTimeOffset).Value = new DateTimeOffset(DateTime.SpecifyKind(row.UpdatedAtUtc, DateTimeKind.Utc));
            command.Parameters.Add("@IsDeleted", SqlDbType.Bit).Value = row.IsDeleted;
            command.Parameters.Add("@DecisionBy", SqlDbType.NVarChar, 200).Value = (object?)row.DecisionBy ?? DBNull.Value;
            command.Parameters.Add("@DecisionAt", SqlDbType.DateTimeOffset).Value = row.DecisionAtUtc.HasValue
                ? new DateTimeOffset(DateTime.SpecifyKind(row.DecisionAtUtc.Value, DateTimeKind.Utc))
                : DBNull.Value;
            command.Parameters.Add("@RejectReason", SqlDbType.NVarChar, 1000).Value = (object?)row.RejectReason ?? DBNull.Value;
            command.Parameters.Add("@OperationId", SqlDbType.UniqueIdentifier).Value = row.OperationId;

            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        private async Task<DateTime> GetWatermarkAsync(CancellationToken cancellationToken)
        {
            await using var connection = new NpgsqlConnection(_supabaseConnectionString);
            await connection.OpenAsync(cancellationToken);

            const string sql = "SELECT \"LastWatermark\" FROM \"SyncState\" WHERE \"Name\" = @Name";
            await using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("@Name", SyncName);

            var result = await command.ExecuteScalarAsync(cancellationToken);
            if (result is DateTime dt)
            {
                return DateTime.SpecifyKind(dt, DateTimeKind.Utc);
            }

            return DateTime.UnixEpoch;
        }

        private async Task SetWatermarkAsync(DateTime watermarkUtc, CancellationToken cancellationToken)
        {
            await using var connection = new NpgsqlConnection(_supabaseConnectionString);
            await connection.OpenAsync(cancellationToken);

            const string sql = @"
INSERT INTO ""SyncState"" (""Name"", ""LastWatermark"", ""UpdatedAt"")
VALUES (@Name, @LastWatermark, NOW())
ON CONFLICT (""Name"") DO UPDATE
SET ""LastWatermark"" = EXCLUDED.""LastWatermark"",
    ""UpdatedAt"" = NOW();";

            await using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("@Name", SyncName);
            command.Parameters.AddWithValue("@LastWatermark", watermarkUtc);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        private async Task<List<SyncSourceRow>> GetChangedRowsAsync(DateTime watermarkUtc, int batchSize, CancellationToken cancellationToken)
        {
            var rows = new List<SyncSourceRow>();

            await using var connection = new NpgsqlConnection(_supabaseConnectionString);
            await connection.OpenAsync(cancellationToken);

            const string sql = @"
SELECT
    ""Id"",
    ""Title"",
    ""RequestedBy"",
    ""Status"",
    ""CreatedAt"",
    ""UpdatedAt"",
    ""IsDeleted"",
    ""DecisionBy"",
    ""DecisionAt"",
    ""RejectReason"",
    ""OperationId""
FROM ""ApprovalRequest""
WHERE ""UpdatedAt"" > @Watermark
ORDER BY ""UpdatedAt"" ASC, ""Id"" ASC
LIMIT @BatchSize;";

            await using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("@Watermark", watermarkUtc);
            command.Parameters.AddWithValue("@BatchSize", batchSize);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                rows.Add(new SyncSourceRow
                {
                    Id = reader.GetInt32(reader.GetOrdinal("Id")),
                    Title = reader.GetString(reader.GetOrdinal("Title")),
                    RequestedBy = reader.GetString(reader.GetOrdinal("RequestedBy")),
                    Status = checked((byte)reader.GetInt16(reader.GetOrdinal("Status"))),
                    CreatedAtUtc = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
                    UpdatedAtUtc = reader.GetDateTime(reader.GetOrdinal("UpdatedAt")),
                    IsDeleted = reader.GetBoolean(reader.GetOrdinal("IsDeleted")),
                    DecisionBy = reader.IsDBNull(reader.GetOrdinal("DecisionBy")) ? null : reader.GetString(reader.GetOrdinal("DecisionBy")),
                    DecisionAtUtc = reader.IsDBNull(reader.GetOrdinal("DecisionAt")) ? null : reader.GetDateTime(reader.GetOrdinal("DecisionAt")),
                    RejectReason = reader.IsDBNull(reader.GetOrdinal("RejectReason")) ? null : reader.GetString(reader.GetOrdinal("RejectReason")),
                    OperationId = reader.IsDBNull(reader.GetOrdinal("OperationId"))
                        ? BuildStableOperationId(reader.GetInt32(reader.GetOrdinal("Id")), reader.GetDateTime(reader.GetOrdinal("UpdatedAt")))
                        : reader.GetGuid(reader.GetOrdinal("OperationId"))
                });
            }

            return rows;
        }

        private static Guid BuildStableOperationId(int id, DateTime updatedAtUtc)
        {
            var bytes = System.Security.Cryptography.MD5.HashData(System.Text.Encoding.UTF8.GetBytes($"{id}:{updatedAtUtc:O}"));
            return new Guid(bytes);
        }

        private async Task LogDeadLetterAsync(SyncSourceRow row, int attemptCount, Exception exception, CancellationToken cancellationToken)
        {
            await using var connection = new NpgsqlConnection(_supabaseConnectionString);
            await connection.OpenAsync(cancellationToken);

            const string sql = @"
INSERT INTO ""SyncDeadLetter"" (""SyncName"", ""EntityId"", ""OperationId"", ""Payload"", ""Error"", ""AttemptCount"", ""CreatedAt"")
VALUES (@SyncName, @EntityId, @OperationId, @Payload::jsonb, @Error, @AttemptCount, NOW());";

            var payload = JsonSerializer.Serialize(row);

            await using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("@SyncName", SyncName);
            command.Parameters.AddWithValue("@EntityId", row.Id);
            command.Parameters.AddWithValue("@OperationId", row.OperationId);
            command.Parameters.AddWithValue("@Payload", payload);
            command.Parameters.AddWithValue("@Error", exception.ToString());
            command.Parameters.AddWithValue("@AttemptCount", attemptCount);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        private async Task MaybeRunDailyReconciliationAsync(CancellationToken cancellationToken)
        {
            if (!IsEnabled)
            {
                return;
            }

            if (!_mssqlReady)
            {
                return;
            }

            var currentDateUtc = DateTime.UtcNow.Date;
            if (_lastReconciliationDateUtc == currentDateUtc)
            {
                return;
            }

            var report = await GenerateReconciliationReportAsync(cancellationToken);
            await SaveReconciliationReportAsync(report, cancellationToken);
            _lastReconciliationDateUtc = currentDateUtc;

            _logger.LogInformation(
                "Daily reconciliation generated. Source={SourceCount}, Target={TargetCount}, MissingInTarget={MissingInTarget}, MissingInSource={MissingInSource}",
                report.SourceCount,
                report.TargetCount,
                report.MissingInTarget,
                report.MissingInSource);
        }

        private async Task<SyncReconciliationResult> GenerateReconciliationReportAsync(CancellationToken cancellationToken)
        {
            var sourceIds = await GetSourceIdsAsync(cancellationToken);
            var targetIds = await GetTargetIdsAsync(cancellationToken);

            var missingInTarget = 0;
            foreach (var id in sourceIds)
            {
                if (!targetIds.Contains(id))
                {
                    missingInTarget++;
                }
            }

            var missingInSource = 0;
            foreach (var id in targetIds)
            {
                if (!sourceIds.Contains(id))
                {
                    missingInSource++;
                }
            }

            return new SyncReconciliationResult
            {
                Enabled = true,
                GeneratedAtUtc = DateTime.UtcNow,
                SourceCount = sourceIds.Count,
                TargetCount = targetIds.Count,
                MissingInTarget = missingInTarget,
                MissingInSource = missingInSource,
                Message = missingInTarget == 0 && missingInSource == 0
                    ? "Source and target are in sync."
                    : "Mismatch detected. Check SyncReconciliationReport for details."
            };
        }

        private async Task<HashSet<int>> GetSourceIdsAsync(CancellationToken cancellationToken)
        {
            var ids = new HashSet<int>();
            await using var connection = new NpgsqlConnection(_supabaseConnectionString);
            await connection.OpenAsync(cancellationToken);

            const string sql = "SELECT \"Id\" FROM \"ApprovalRequest\"";
            await using var command = new NpgsqlCommand(sql, connection);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                ids.Add(reader.GetInt32(0));
            }

            return ids;
        }

        private async Task<HashSet<int>> GetTargetIdsAsync(CancellationToken cancellationToken)
        {
            var ids = new HashSet<int>();
            await using var connection = new SqlConnection(_mssqlConnectionString);
            await connection.OpenAsync(cancellationToken);

            const string sql = "SELECT Id FROM dbo.ApprovalRequestMirror";
            await using var command = new SqlCommand(sql, connection);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                ids.Add(reader.GetInt32(0));
            }

            return ids;
        }

        private async Task<HashSet<int>> GetStudentSourceIdsAsync(CancellationToken cancellationToken)
        {
            var ids = new HashSet<int>();
            await using var connection = new NpgsqlConnection(_supabaseConnectionString);
            await connection.OpenAsync(cancellationToken);

            const string sql = "SELECT \"Id\" FROM \"StudentDirectory\"";
            await using var command = new NpgsqlCommand(sql, connection);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                ids.Add(reader.GetInt32(0));
            }

            return ids;
        }

        private async Task<HashSet<int>> GetStudentTargetIdsAsync(CancellationToken cancellationToken)
        {
            var ids = new HashSet<int>();
            await using var connection = new SqlConnection(_mssqlConnectionString);
            await connection.OpenAsync(cancellationToken);

            const string sql = "SELECT Id FROM dbo.StudentDirectoryMirror";
            await using var command = new SqlCommand(sql, connection);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                ids.Add(reader.GetInt32(0));
            }

            return ids;
        }

        private async Task<HashSet<int>> GetStaffSourceIdsAsync(CancellationToken cancellationToken)
        {
            var ids = new HashSet<int>();
            await using var connection = new NpgsqlConnection(_supabaseConnectionString);
            await connection.OpenAsync(cancellationToken);

            const string sql = "SELECT \"Id\" FROM \"StaffDirectory\"";
            await using var command = new NpgsqlCommand(sql, connection);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                ids.Add(reader.GetInt32(0));
            }

            return ids;
        }

        private async Task<HashSet<int>> GetStaffTargetIdsAsync(CancellationToken cancellationToken)
        {
            var ids = new HashSet<int>();
            await using var connection = new SqlConnection(_mssqlConnectionString);
            await connection.OpenAsync(cancellationToken);

            const string sql = "SELECT Id FROM dbo.StaffDirectoryMirror";
            await using var command = new SqlCommand(sql, connection);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                ids.Add(reader.GetInt32(0));
            }

            return ids;
        }

        private async Task<HashSet<Guid>> GetTlAssignmentSourceIdsAsync(CancellationToken cancellationToken)
        {
            var ids = new HashSet<Guid>();
            await using var connection = new NpgsqlConnection(_supabaseConnectionString);
            await connection.OpenAsync(cancellationToken);

            const string sql = "SELECT \"Id\" FROM \"TlTeamAssignment\"";
            await using var command = new NpgsqlCommand(sql, connection);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                ids.Add(reader.GetGuid(0));
            }

            return ids;
        }

        private async Task<HashSet<Guid>> GetTlAssignmentTargetIdsAsync(CancellationToken cancellationToken)
        {
            var ids = new HashSet<Guid>();
            await using var connection = new SqlConnection(_mssqlConnectionString);
            await connection.OpenAsync(cancellationToken);

            const string sql = "SELECT Id FROM dbo.TlTeamAssignmentMirror";
            await using var command = new SqlCommand(sql, connection);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                ids.Add(reader.GetGuid(0));
            }

            return ids;
        }

        private async Task SaveReconciliationReportAsync(SyncReconciliationResult report, CancellationToken cancellationToken)
        {
            await using var connection = new NpgsqlConnection(_supabaseConnectionString);
            await connection.OpenAsync(cancellationToken);

            const string sql = @"
INSERT INTO ""SyncReconciliationReport"" (""SyncName"", ""GeneratedAt"", ""SourceCount"", ""TargetCount"", ""MissingInTarget"", ""MissingInSource"", ""Summary"")
VALUES (@SyncName, @GeneratedAt, @SourceCount, @TargetCount, @MissingInTarget, @MissingInSource, @Summary);";

            await using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("@SyncName", SyncName);
            command.Parameters.AddWithValue("@GeneratedAt", report.GeneratedAtUtc);
            command.Parameters.AddWithValue("@SourceCount", report.SourceCount);
            command.Parameters.AddWithValue("@TargetCount", report.TargetCount);
            command.Parameters.AddWithValue("@MissingInTarget", report.MissingInTarget);
            command.Parameters.AddWithValue("@MissingInSource", report.MissingInSource);
            command.Parameters.AddWithValue("@Summary", report.Message);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        private async Task EnsureSupabaseSchemaAsync(CancellationToken cancellationToken)
        {
            await using var connection = new NpgsqlConnection(_supabaseConnectionString);
            await connection.OpenAsync(cancellationToken);

            const string sql = @"
CREATE EXTENSION IF NOT EXISTS pgcrypto;

ALTER TABLE ""ApprovalRequest"" ADD COLUMN IF NOT EXISTS ""UpdatedAt"" timestamptz;
ALTER TABLE ""ApprovalRequest"" ADD COLUMN IF NOT EXISTS ""IsDeleted"" boolean;
ALTER TABLE ""ApprovalRequest"" ADD COLUMN IF NOT EXISTS ""OperationId"" uuid;

UPDATE ""ApprovalRequest"" SET ""UpdatedAt"" = COALESCE(""UpdatedAt"", ""CreatedAt"", NOW());
UPDATE ""ApprovalRequest"" SET ""IsDeleted"" = COALESCE(""IsDeleted"", FALSE);
UPDATE ""ApprovalRequest"" SET ""OperationId"" = COALESCE(""OperationId"", gen_random_uuid());

ALTER TABLE ""ApprovalRequest"" ALTER COLUMN ""UpdatedAt"" SET NOT NULL;
ALTER TABLE ""ApprovalRequest"" ALTER COLUMN ""IsDeleted"" SET NOT NULL;
ALTER TABLE ""ApprovalRequest"" ALTER COLUMN ""OperationId"" SET NOT NULL;

CREATE INDEX IF NOT EXISTS idx_approvalrequest_updatedat ON ""ApprovalRequest"" (""UpdatedAt"");

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

CREATE TABLE IF NOT EXISTS ""SyncState"" (
    ""Name"" text PRIMARY KEY,
    ""LastWatermark"" timestamptz NOT NULL DEFAULT to_timestamp(0),
    ""UpdatedAt"" timestamptz NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS ""SyncDeadLetter"" (
    ""Id"" bigserial PRIMARY KEY,
    ""SyncName"" text NOT NULL,
    ""EntityId"" integer NOT NULL,
    ""OperationId"" uuid NOT NULL,
    ""Payload"" jsonb NOT NULL,
    ""Error"" text NOT NULL,
    ""AttemptCount"" integer NOT NULL,
    ""CreatedAt"" timestamptz NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS ""SyncReconciliationReport"" (
    ""Id"" bigserial PRIMARY KEY,
    ""SyncName"" text NOT NULL,
    ""GeneratedAt"" timestamptz NOT NULL,
    ""SourceCount"" integer NOT NULL,
    ""TargetCount"" integer NOT NULL,
    ""MissingInTarget"" integer NOT NULL,
    ""MissingInSource"" integer NOT NULL,
    ""Summary"" text NOT NULL
);

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

            await using var command = new NpgsqlCommand(sql, connection);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        private async Task EnsureMssqlSchemaAsync(CancellationToken cancellationToken)
        {
            await using var connection = new SqlConnection(_mssqlConnectionString);
            await connection.OpenAsync(cancellationToken);

            const string sql = @"
IF OBJECT_ID(N'dbo.ApprovalRequestMirror', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ApprovalRequestMirror (
        Id INT NOT NULL PRIMARY KEY,
        Title NVARCHAR(500) NOT NULL,
        RequestedBy NVARCHAR(200) NOT NULL,
        Status TINYINT NOT NULL,
        CreatedAt DATETIMEOFFSET(0) NOT NULL,
        UpdatedAt DATETIMEOFFSET(0) NOT NULL,
        IsDeleted BIT NOT NULL,
        DecisionBy NVARCHAR(200) NULL,
        DecisionAt DATETIMEOFFSET(0) NULL,
        RejectReason NVARCHAR(1000) NULL,
        OperationId UNIQUEIDENTIFIER NOT NULL,
        LastSyncedAt DATETIMEOFFSET(0) NOT NULL CONSTRAINT DF_ApprovalRequestMirror_LastSyncedAt DEFAULT SYSUTCDATETIME()
    );
END;

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_ApprovalRequestMirror_UpdatedAt'
      AND object_id = OBJECT_ID(N'dbo.ApprovalRequestMirror')
)
BEGIN
    CREATE INDEX IX_ApprovalRequestMirror_UpdatedAt ON dbo.ApprovalRequestMirror(UpdatedAt);
END;";

            const string studentSql = @"
IF OBJECT_ID(N'dbo.StudentDirectoryMirror', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.StudentDirectoryMirror (
        Id INT NOT NULL PRIMARY KEY,
        StudentCode NVARCHAR(50) NOT NULL,
        FullName NVARCHAR(200) NOT NULL,
        GradeName NVARCHAR(50) NOT NULL,
        SectionName NVARCHAR(50) NOT NULL,
        PhotoUrl NVARCHAR(MAX) NULL,
        IsActive BIT NOT NULL,
        UpdatedAt DATETIMEOFFSET(0) NOT NULL,
        LastSyncedAt DATETIMEOFFSET(0) NOT NULL CONSTRAINT DF_StudentDirectoryMirror_LastSyncedAt DEFAULT SYSUTCDATETIME()
    );
END;

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_StudentDirectoryMirror_Grade_Section'
      AND object_id = OBJECT_ID(N'dbo.StudentDirectoryMirror')
)
BEGIN
    CREATE INDEX IX_StudentDirectoryMirror_Grade_Section ON dbo.StudentDirectoryMirror(GradeName, SectionName);
END;

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_StudentDirectoryMirror_Active_FullName'
      AND object_id = OBJECT_ID(N'dbo.StudentDirectoryMirror')
)
BEGIN
    CREATE INDEX IX_StudentDirectoryMirror_Active_FullName ON dbo.StudentDirectoryMirror(IsActive, FullName);
END;

IF OBJECT_ID(N'dbo.StaffDirectoryMirror', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.StaffDirectoryMirror (
        Id INT NOT NULL PRIMARY KEY,
        StaffCode NVARCHAR(50) NOT NULL,
        FullName NVARCHAR(200) NOT NULL,
        DepartmentName NVARCHAR(100) NOT NULL,
        TeamName NVARCHAR(100) NOT NULL,
        Designation NVARCHAR(120) NOT NULL,
        PhotoUrl NVARCHAR(MAX) NULL,
        IsActive BIT NOT NULL,
        IsSystemAccount BIT NOT NULL,
        UpdatedAt DATETIMEOFFSET(0) NOT NULL,
        LastSyncedAt DATETIMEOFFSET(0) NOT NULL CONSTRAINT DF_StaffDirectoryMirror_LastSyncedAt DEFAULT SYSUTCDATETIME()
    );
END;

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_StaffDirectoryMirror_Active_System_FullName'
      AND object_id = OBJECT_ID(N'dbo.StaffDirectoryMirror')
)
BEGIN
    CREATE INDEX IX_StaffDirectoryMirror_Active_System_FullName ON dbo.StaffDirectoryMirror(IsActive, IsSystemAccount, FullName);
END;

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_StaffDirectoryMirror_Department_Team'
      AND object_id = OBJECT_ID(N'dbo.StaffDirectoryMirror')
)
BEGIN
    CREATE INDEX IX_StaffDirectoryMirror_Department_Team ON dbo.StaffDirectoryMirror(DepartmentName, TeamName);
END;

IF OBJECT_ID(N'dbo.TlTeamAssignmentMirror', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.TlTeamAssignmentMirror (
        Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        TlStaffCode NVARCHAR(50) NOT NULL,
        DepartmentName NVARCHAR(100) NOT NULL,
        TeamName NVARCHAR(100) NOT NULL,
        MemberStaffIdsJson NVARCHAR(MAX) NOT NULL,
        TaskDescription NVARCHAR(MAX) NULL,
        CreatedAt DATETIMEOFFSET(0) NOT NULL,
        UpdatedAt DATETIMEOFFSET(0) NOT NULL,
        LastSyncedAt DATETIMEOFFSET(0) NOT NULL CONSTRAINT DF_TlTeamAssignmentMirror_LastSyncedAt DEFAULT SYSUTCDATETIME()
    );
END;

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_TlTeamAssignmentMirror_TlStaffCode'
      AND object_id = OBJECT_ID(N'dbo.TlTeamAssignmentMirror')
)
BEGIN
    CREATE INDEX IX_TlTeamAssignmentMirror_TlStaffCode ON dbo.TlTeamAssignmentMirror(TlStaffCode);
END;

IF COL_LENGTH('dbo.StudentDirectoryMirror', 'ProgramCode') IS NULL ALTER TABLE dbo.StudentDirectoryMirror ADD ProgramCode NVARCHAR(50) NULL;

IF COL_LENGTH('dbo.StudentDirectoryMirror', 'AcademicYearCode') IS NULL
    ALTER TABLE dbo.StudentDirectoryMirror ADD AcademicYearCode NVARCHAR(20) NOT NULL CONSTRAINT DF_StudentDirectoryMirror_AcademicYear DEFAULT '2025-26';

IF COL_LENGTH('dbo.StaffDirectoryMirror', 'StaffCategory') IS NULL
    ALTER TABLE dbo.StaffDirectoryMirror ADD StaffCategory NVARCHAR(30) NOT NULL CONSTRAINT DF_StaffDirectoryMirror_StaffCategory DEFAULT 'Academic';

IF OBJECT_ID(N'dbo.AcademicProgramMirror', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AcademicProgramMirror (
        Id INT NOT NULL PRIMARY KEY,
        Code NVARCHAR(50) NOT NULL,
        Name NVARCHAR(200) NOT NULL,
        IsActive BIT NOT NULL,
        UpdatedAt DATETIMEOFFSET(0) NOT NULL,
        LastSyncedAt DATETIMEOFFSET(0) NOT NULL CONSTRAINT DF_AcademicProgramMirror_LastSyncedAt DEFAULT SYSUTCDATETIME()
    );
END;

IF OBJECT_ID(N'dbo.AcademicTermMirror', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AcademicTermMirror (
        Id INT NOT NULL PRIMARY KEY,
        TermCode NVARCHAR(50) NOT NULL,
        TermName NVARCHAR(200) NOT NULL,
        SchoolYearCode NVARCHAR(20) NOT NULL,
        StartDate DATE NOT NULL,
        EndDate DATE NOT NULL,
        IsInCurrentSchoolYear BIT NOT NULL,
        UpdatedAt DATETIMEOFFSET(0) NOT NULL,
        LastSyncedAt DATETIMEOFFSET(0) NOT NULL CONSTRAINT DF_AcademicTermMirror_LastSyncedAt DEFAULT SYSUTCDATETIME()
    );
END;

IF OBJECT_ID(N'dbo.GradeSectionMentorMirror', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.GradeSectionMentorMirror (
        Id INT NOT NULL PRIMARY KEY,
        GradeName NVARCHAR(80) NOT NULL,
        SectionName NVARCHAR(80) NOT NULL,
        MentorStaffCode NVARCHAR(50) NOT NULL,
        MentorFullName NVARCHAR(200) NOT NULL,
        UpdatedAt DATETIMEOFFSET(0) NOT NULL,
        LastSyncedAt DATETIMEOFFSET(0) NOT NULL CONSTRAINT DF_GradeSectionMentorMirror_LastSyncedAt DEFAULT SYSUTCDATETIME()
    );
END;

IF OBJECT_ID(N'dbo.HrisLeaveBalanceMirror', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.HrisLeaveBalanceMirror (
        Id INT NOT NULL PRIMARY KEY,
        StaffCode NVARCHAR(50) NOT NULL,
        LeaveType NVARCHAR(80) NOT NULL,
        BalanceDays DECIMAL(6,2) NOT NULL,
        AsOfDate DATE NOT NULL,
        UpdatedAt DATETIMEOFFSET(0) NOT NULL,
        LastSyncedAt DATETIMEOFFSET(0) NOT NULL CONSTRAINT DF_HrisLeaveBalanceMirror_LastSyncedAt DEFAULT SYSUTCDATETIME()
    );
END;

IF OBJECT_ID(N'dbo.sp_Task8_ActiveStudentsDetail', N'P') IS NOT NULL
    DROP PROCEDURE dbo.sp_Task8_ActiveStudentsDetail;

EXEC(N'CREATE PROCEDURE dbo.sp_Task8_ActiveStudentsDetail
AS
BEGIN
    SET NOCOUNT ON;
    SELECT FullName, GradeName, SectionName, AcademicYearCode
    FROM dbo.StudentDirectoryMirror
    WHERE IsActive = 1
    ORDER BY FullName;
END');";

            await using var command = new SqlCommand(sql, connection);
            await command.ExecuteNonQueryAsync(cancellationToken);

            await using var studentCommand = new SqlCommand(studentSql, connection);
            await studentCommand.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    public sealed class SyncRunResult
    {
        public bool Enabled { get; init; }
        public DateTime WatermarkFromUtc { get; init; }
        public DateTime WatermarkToUtc { get; init; }
        public int Processed { get; init; }
        public int Successful { get; init; }
        public int Failed { get; init; }
        public string Message { get; init; } = string.Empty;

        public static SyncRunResult Disabled(string message) => new()
        {
            Enabled = false,
            WatermarkFromUtc = DateTime.MinValue,
            WatermarkToUtc = DateTime.MinValue,
            Message = message
        };
    }

    public sealed class SyncReconciliationResult
    {
        public bool Enabled { get; init; }
        public DateTime GeneratedAtUtc { get; init; }
        public int SourceCount { get; init; }
        public int TargetCount { get; init; }
        public int MissingInTarget { get; init; }
        public int MissingInSource { get; init; }
        public string Message { get; init; } = string.Empty;
    }

    internal sealed class SyncSourceRow
    {
        public int Id { get; init; }
        public string Title { get; init; } = string.Empty;
        public string RequestedBy { get; init; } = string.Empty;
        public byte Status { get; init; }
        public DateTime CreatedAtUtc { get; init; }
        public DateTime UpdatedAtUtc { get; init; }
        public bool IsDeleted { get; init; }
        public string? DecisionBy { get; init; }
        public DateTime? DecisionAtUtc { get; init; }
        public string? RejectReason { get; init; }
        public Guid OperationId { get; init; }
    }

    internal sealed class StudentSourceRow
    {
        public int Id { get; init; }
        public string StudentCode { get; init; } = string.Empty;
        public string FullName { get; init; } = string.Empty;
        public string GradeName { get; init; } = string.Empty;
        public string SectionName { get; init; } = string.Empty;
        public string? PhotoUrl { get; init; }
        public bool IsActive { get; init; }
        public string? ProgramCode { get; init; }
        public string AcademicYearCode { get; init; } = "2025-26";
        public DateTime UpdatedAtUtc { get; init; }
    }

    internal sealed class StaffSourceRow
    {
        public int Id { get; init; }
        public string StaffCode { get; init; } = string.Empty;
        public string FullName { get; init; } = string.Empty;
        public string DepartmentName { get; init; } = string.Empty;
        public string TeamName { get; init; } = string.Empty;
        public string Designation { get; init; } = string.Empty;
        public string? PhotoUrl { get; init; }
        public bool IsActive { get; init; }
        public bool IsSystemAccount { get; init; }
        public string StaffCategory { get; init; } = "Academic";
        public DateTime UpdatedAtUtc { get; init; }
    }

    internal sealed class TlAssignmentSourceRow
    {
        public Guid Id { get; init; }
        public string TlStaffCode { get; init; } = string.Empty;
        public string DepartmentName { get; init; } = string.Empty;
        public string TeamName { get; init; } = string.Empty;
        public int[] MemberStaffIds { get; init; } = Array.Empty<int>();
        public string? TaskDescription { get; init; }
        public DateTime CreatedAtUtc { get; init; }
        public DateTime UpdatedAtUtc { get; init; }
    }
}
