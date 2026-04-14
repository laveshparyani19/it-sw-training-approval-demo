-- Task 8: Academic programs, terms, mentors, HRIS leave (Supabase)
-- Optional reference; API also runs Task8Repository.EnsureSchemaAndSeedAsync on startup.

CREATE TABLE IF NOT EXISTS "AcademicProgram" (
    "Id" SERIAL PRIMARY KEY,
    "Code" VARCHAR(50) NOT NULL UNIQUE,
    "Name" VARCHAR(200) NOT NULL,
    "IsActive" BOOLEAN NOT NULL DEFAULT TRUE,
    "UpdatedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS "AcademicTerm" (
    "Id" SERIAL PRIMARY KEY,
    "TermCode" VARCHAR(50) NOT NULL,
    "TermName" VARCHAR(200) NOT NULL,
    "SchoolYearCode" VARCHAR(20) NOT NULL,
    "StartDate" DATE NOT NULL,
    "EndDate" DATE NOT NULL,
    "IsInCurrentSchoolYear" BOOLEAN NOT NULL DEFAULT FALSE,
    "UpdatedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE ("TermCode", "SchoolYearCode")
);

CREATE TABLE IF NOT EXISTS "GradeSectionMentor" (
    "Id" SERIAL PRIMARY KEY,
    "GradeName" VARCHAR(80) NOT NULL,
    "SectionName" VARCHAR(80) NOT NULL,
    "MentorStaffCode" VARCHAR(50) NOT NULL,
    "MentorFullName" VARCHAR(200) NOT NULL,
    "UpdatedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE ("GradeName", "SectionName")
);

CREATE TABLE IF NOT EXISTS "HrisLeaveBalance" (
    "Id" SERIAL PRIMARY KEY,
    "StaffCode" VARCHAR(50) NOT NULL,
    "LeaveType" VARCHAR(80) NOT NULL,
    "BalanceDays" NUMERIC(6,2) NOT NULL,
    "AsOfDate" DATE NOT NULL,
    "UpdatedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE ("StaffCode", "LeaveType", "AsOfDate")
);

ALTER TABLE "StudentDirectory" ADD COLUMN IF NOT EXISTS "ProgramCode" VARCHAR(50) NULL;
ALTER TABLE "StudentDirectory" ADD COLUMN IF NOT EXISTS "AcademicYearCode" VARCHAR(20) NOT NULL DEFAULT '2025-26';

ALTER TABLE "StaffDirectory" ADD COLUMN IF NOT EXISTS "StaffCategory" VARCHAR(30) NOT NULL DEFAULT 'Academic';
