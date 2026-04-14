-- Task 8: Mirror tables + stored procedure (reporting SQL Server)
-- Also applied via ApprovalSyncService.EnsureMssqlSchemaAsync when sync is enabled.

IF COL_LENGTH('dbo.StudentDirectoryMirror', 'ProgramCode') IS NULL
    ALTER TABLE dbo.StudentDirectoryMirror ADD ProgramCode NVARCHAR(50) NULL;

IF COL_LENGTH('dbo.StudentDirectoryMirror', 'AcademicYearCode') IS NULL
    ALTER TABLE dbo.StudentDirectoryMirror ADD AcademicYearCode NVARCHAR(20) NOT NULL
        CONSTRAINT DF_StudentDirectoryMirror_AcademicYear DEFAULT '2025-26';

IF COL_LENGTH('dbo.StaffDirectoryMirror', 'StaffCategory') IS NULL
    ALTER TABLE dbo.StaffDirectoryMirror ADD StaffCategory NVARCHAR(30) NOT NULL
        CONSTRAINT DF_StaffDirectoryMirror_StaffCategory DEFAULT 'Academic';

IF OBJECT_ID(N'dbo.AcademicProgramMirror', N'U') IS NULL
CREATE TABLE dbo.AcademicProgramMirror (
    Id INT NOT NULL PRIMARY KEY,
    Code NVARCHAR(50) NOT NULL,
    Name NVARCHAR(200) NOT NULL,
    IsActive BIT NOT NULL,
    UpdatedAt DATETIMEOFFSET(0) NOT NULL,
    LastSyncedAt DATETIMEOFFSET(0) NOT NULL CONSTRAINT DF_AcademicProgramMirror_LastSyncedAt DEFAULT SYSUTCDATETIME()
);

IF OBJECT_ID(N'dbo.AcademicTermMirror', N'U') IS NULL
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

IF OBJECT_ID(N'dbo.GradeSectionMentorMirror', N'U') IS NULL
CREATE TABLE dbo.GradeSectionMentorMirror (
    Id INT NOT NULL PRIMARY KEY,
    GradeName NVARCHAR(80) NOT NULL,
    SectionName NVARCHAR(80) NOT NULL,
    MentorStaffCode NVARCHAR(50) NOT NULL,
    MentorFullName NVARCHAR(200) NOT NULL,
    UpdatedAt DATETIMEOFFSET(0) NOT NULL,
    LastSyncedAt DATETIMEOFFSET(0) NOT NULL CONSTRAINT DF_GradeSectionMentorMirror_LastSyncedAt DEFAULT SYSUTCDATETIME()
);

IF OBJECT_ID(N'dbo.HrisLeaveBalanceMirror', N'U') IS NULL
CREATE TABLE dbo.HrisLeaveBalanceMirror (
    Id INT NOT NULL PRIMARY KEY,
    StaffCode NVARCHAR(50) NOT NULL,
    LeaveType NVARCHAR(80) NOT NULL,
    BalanceDays DECIMAL(6,2) NOT NULL,
    AsOfDate DATE NOT NULL,
    UpdatedAt DATETIMEOFFSET(0) NOT NULL,
    LastSyncedAt DATETIMEOFFSET(0) NOT NULL CONSTRAINT DF_HrisLeaveBalanceMirror_LastSyncedAt DEFAULT SYSUTCDATETIME()
);
GO

CREATE OR ALTER PROCEDURE dbo.sp_Task8_ActiveStudentsDetail
AS
BEGIN
    SET NOCOUNT ON;
    SELECT FullName, GradeName, SectionName, AcademicYearCode
    FROM dbo.StudentDirectoryMirror
    WHERE IsActive = 1
    ORDER BY FullName;
END
GO
