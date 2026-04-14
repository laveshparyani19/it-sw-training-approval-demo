-- Task 11: TL team assignment mirror (SQL Server) — synced from Supabase by ApprovalSyncService
-- Run against your reporting / mirror database when ENABLE_MSSQL_SYNC is enabled.

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
