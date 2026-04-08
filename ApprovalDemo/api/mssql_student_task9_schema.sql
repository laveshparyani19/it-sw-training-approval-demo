-- Task 9: Student directory (MSSQL mirror/reporting)
-- Run this in your own MSSQL database only.

IF OBJECT_ID('dbo.StudentDirectoryMirror', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.StudentDirectoryMirror
    (
        Id INT NOT NULL PRIMARY KEY,
        StudentCode NVARCHAR(50) NOT NULL UNIQUE,
        FullName NVARCHAR(200) NOT NULL,
        GradeName NVARCHAR(50) NOT NULL,
        SectionName NVARCHAR(50) NOT NULL,
        PhotoUrl NVARCHAR(1000) NULL,
        IsActive BIT NOT NULL CONSTRAINT DF_StudentDirectoryMirror_IsActive DEFAULT (1),
        UpdatedAt DATETIMEOFFSET NOT NULL,
        LastSyncedAt DATETIMEOFFSET NOT NULL CONSTRAINT DF_StudentDirectoryMirror_LastSyncedAt DEFAULT (SYSUTCDATETIME())
    );
END
GO

IF NOT EXISTS (SELECT 1
FROM sys.indexes
WHERE name = 'IX_StudentDirectoryMirror_GradeSection' AND object_id = OBJECT_ID('dbo.StudentDirectoryMirror'))
BEGIN
    CREATE INDEX IX_StudentDirectoryMirror_GradeSection
        ON dbo.StudentDirectoryMirror (GradeName, SectionName);
END
GO

IF NOT EXISTS (SELECT 1
FROM sys.indexes
WHERE name = 'IX_StudentDirectoryMirror_ActiveName' AND object_id = OBJECT_ID('dbo.StudentDirectoryMirror'))
BEGIN
    CREATE INDEX IX_StudentDirectoryMirror_ActiveName
        ON dbo.StudentDirectoryMirror (IsActive, FullName);
END
GO
