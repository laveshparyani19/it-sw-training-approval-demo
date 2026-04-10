-- Task 10: Staff directory (MSSQL mirror/reporting)
-- Run this in your MSSQL database.

IF OBJECT_ID('dbo.StaffDirectoryMirror', 'U') IS NULL
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
        IsActive BIT NOT NULL CONSTRAINT DF_StaffDirectoryMirror_IsActive DEFAULT (1),
        IsSystemAccount BIT NOT NULL CONSTRAINT DF_StaffDirectoryMirror_IsSystemAccount DEFAULT (0),
        UpdatedAt DATETIMEOFFSET NOT NULL,
        LastSyncedAt DATETIMEOFFSET NOT NULL CONSTRAINT DF_StaffDirectoryMirror_LastSyncedAt DEFAULT (SYSUTCDATETIME())
    );
END
GO

IF NOT EXISTS (SELECT 1
FROM sys.indexes
WHERE name = 'IX_StaffDirectoryMirror_Active_System_FullName' AND object_id = OBJECT_ID('dbo.StaffDirectoryMirror'))
BEGIN
    CREATE INDEX IX_StaffDirectoryMirror_Active_System_FullName
        ON dbo.StaffDirectoryMirror (IsActive, IsSystemAccount, FullName);
END
GO

IF NOT EXISTS (SELECT 1
FROM sys.indexes
WHERE name = 'IX_StaffDirectoryMirror_Department_Team' AND object_id = OBJECT_ID('dbo.StaffDirectoryMirror'))
BEGIN
    CREATE INDEX IX_StaffDirectoryMirror_Department_Team
        ON dbo.StaffDirectoryMirror (DepartmentName, TeamName);
END
GO
