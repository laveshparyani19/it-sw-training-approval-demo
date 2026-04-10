-- MSSQL reporting mirror schema for ApprovalDemo
-- Run on the SQL Server database used for reporting/backup.

IF OBJECT_ID(N'dbo.ApprovalRequestMirror', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ApprovalRequestMirror
    (
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
END;

IF OBJECT_ID(N'dbo.StaffDirectoryMirror', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.StaffDirectoryMirror
    (
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
