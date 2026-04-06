using System;
using System.Collections.Generic;
using System.Data;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace ApprovalDemo.Api.Services
{
    public sealed class ApprovalSyncService
    {
        private const string SyncName = "ApprovalToMssql";
        private readonly string _supabaseConnectionString;
        private readonly string? _mssqlConnectionString;
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
            _mssqlReady = !string.IsNullOrWhiteSpace(_mssqlConnectionString);
            if (!_mssqlReady)
            {
                _mssqlDisabledReason = "MSSQL_CONNECTION_STRING is not configured.";
            }
        }

        public bool IsEnabled => !string.IsNullOrWhiteSpace(_mssqlConnectionString);

        public async Task InitializeAsync(CancellationToken cancellationToken)
        {
            await EnsureSupabaseSchemaAsync(cancellationToken);
            if (IsEnabled)
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
                    await MaybeRunDailyReconciliationAsync(cancellationToken);
                    return new SyncRunResult
                    {
                        Enabled = true,
                        WatermarkFromUtc = currentWatermark,
                        WatermarkToUtc = currentWatermark,
                        Processed = 0,
                        Successful = 0,
                        Failed = 0,
                        Message = "No changed rows found."
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
                        : "Sync completed successfully."
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
);";

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

            await using var command = new SqlCommand(sql, connection);
            await command.ExecuteNonQueryAsync(cancellationToken);
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
}
