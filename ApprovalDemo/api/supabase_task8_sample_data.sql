-- =============================================================================
-- Task 8 — Rich sample data for ApprovalDemo (Supabase / PostgreSQL)
-- =============================================================================
-- Structural reference ONLY (read-only patterns from LIVE_PROJ/INVENTORY.MDF):
--   dbo.Program        → ProgramName, ProgramFullName (VARCHAR(50))
--   dbo.AcademicTerms  → TermName, programme-specific From/To date bands
--   dbo.Student        → RollNo, FirstName, LastName, CurrentYear (VARCHAR(20))
-- This script does NOT attach to or modify INVENTORY.MDF.
--
-- Prerequisites: tables exist (run supabase_task8_schema.sql, or start the API once).
--
-- After loading this data in Supabase, mirror to SQL Server by running sync:
--   From repo root:  dotnet run --project ApprovalDemo/sync-worker
--   With ApprovalDemo/.env containing Supabase URL + password, MSSQL_CONNECTION_STRING,
--   and ENABLE_MSSQL_SYNC=true (see ApprovalDemo/sync-worker/README.txt).
--   Alternatively: POST /api/sync/run on an API host that can reach your SQL Server.
-- =============================================================================

-- Programmes (names shortened like INVENTORY Program.ProgramName / ProgramFullName style)
INSERT INTO "AcademicProgram" ("Code", "Name", "IsActive", "UpdatedAt")
VALUES
('PRG-PYP', 'Primary Years Programme (PYP)', TRUE, NOW()),
('PRG-MYP', 'Middle Years Programme (MYP)', TRUE, NOW()),
('PRG-DP', 'Diploma Programme (DP)', TRUE, NOW()),
('PRG-EYP', 'Early Years Programme', TRUE, NOW()),
('PRG-HS', 'High School Pathway', FALSE, NOW()),
('PRG-CP', 'Career-related Programme', TRUE, NOW())
ON CONFLICT ("Code") DO UPDATE SET
 "Name" = EXCLUDED."Name",
  "IsActive" = EXCLUDED."IsActive",
  "UpdatedAt" = NOW();

-- Terms (single consolidated row per term; dates loosely mirror AcademicTerms banding)
INSERT INTO "AcademicTerm" ("TermCode", "TermName", "SchoolYearCode", "StartDate", "EndDate", "IsInCurrentSchoolYear", "UpdatedAt")
VALUES
('T1', 'Term 1', '2025-26', DATE '2025-08-04', DATE '2025-12-19', TRUE, NOW()),
('T2', 'Term 2', '2025-26', DATE '2026-01-05', DATE '2026-04-10', TRUE, NOW()),
('T3', 'Term 3', '2025-26', DATE '2026-04-13', DATE '2026-06-26', TRUE, NOW()),
('T1', 'Term 1', '2024-25', DATE '2024-08-05', DATE '2024-12-20', FALSE, NOW())
ON CONFLICT ("TermCode", "SchoolYearCode") DO UPDATE SET
  "TermName" = EXCLUDED."TermName",
  "StartDate" = EXCLUDED."StartDate",
  "EndDate" = EXCLUDED."EndDate",
  "IsInCurrentSchoolYear" = EXCLUDED."IsInCurrentSchoolYear",
  "UpdatedAt" = NOW();

-- Students: RollNo-style codes + FullName like FirstName + LastName; CurrentYear → AcademicYearCode
INSERT INTO "StudentDirectory" ("StudentCode", "FullName", "GradeName", "SectionName", "PhotoUrl", "IsActive", "ProgramCode", "AcademicYearCode", "UpdatedAt")
VALUES
('FSK2026041', 'Vivaan Khanna', 'Grade 4', 'G4', 'https://i.pravatar.cc/160?img=21', TRUE, 'PRG-PYP', '2025-26', NOW()),
('FSK2026042', 'Anika Reddy', 'Grade 4', 'G4', 'https://i.pravatar.cc/160?img=22', TRUE, 'PRG-PYP', '2025-26', NOW()),
('FSK2025131', 'Reyansh Iyer', 'Grade 5', 'Mavericks - 5', 'https://i.pravatar.cc/160?img=23', TRUE, 'PRG-PYP', '2025-26', NOW()),
('FSK2025132', 'Kiara Menon', 'Grade 5', 'Collaboration', 'https://i.pravatar.cc/160?img=24', TRUE, 'PRG-PYP', '2025-26', NOW()),
('FSK2024121', 'Arnav Saxena', 'Grade 6', 'G6', 'https://i.pravatar.cc/160?img=25', TRUE, 'PRG-MYP', '2025-26', NOW()),
('FSK2024122', 'Pari Bhatt', 'Grade 6', 'G6', NULL, TRUE, 'PRG-MYP', '2025-26', NOW()),
('FSK2023111', 'Dhruv Nambiar', 'Grade 7', 'G7', 'https://i.pravatar.cc/160?img=26', TRUE, 'PRG-MYP', '2025-26', NOW()),
('FSK2023112', 'Sara Qureshi', 'Grade 7', 'G7', 'https://i.pravatar.cc/160?img=27', TRUE, 'PRG-MYP', '2025-26', NOW()),
('FSK2022101', 'Ishan Kulkarni', 'Grade 8', 'G8', 'https://i.pravatar.cc/160?img=28', TRUE, 'PRG-MYP', '2025-26', NOW()),
('FSK2022102', 'Meera Pillai', 'Grade 8', 'G8', 'https://i.pravatar.cc/160?img=29', TRUE, 'PRG-MYP', '2025-26', NOW()),
('FSK2021091', 'Rohan Varma', 'Grade 9', 'G9', 'https://i.pravatar.cc/160?img=30', TRUE, 'PRG-MYP', '2025-26', NOW()),
('FSK2020301', 'Tara Krishnan', 'Grade 10', 'G10', 'https://i.pravatar.cc/160?img=31', TRUE, 'PRG-MYP', '2025-26', NOW()),
('FSK2019211', 'Kunal Bose', 'Grade 11', 'Mavericks - 11', 'https://i.pravatar.cc/160?img=32', TRUE, 'PRG-DP', '2025-26', NOW()),
('FSK2019212', 'Nandini Rao', 'Grade 11', 'Aplomb', 'https://i.pravatar.cc/160?img=33', TRUE, 'PRG-DP', '2025-26', NOW()),
('FSK2018221', 'Aditya Sen', 'Grade 12', 'Mavericks - 12', 'https://i.pravatar.cc/160?img=34', TRUE, 'PRG-DP', '2025-26', NOW()),
('FSK2018222', 'Ishita Malhotra', 'Grade 12', 'Sangfroid', 'https://i.pravatar.cc/160?img=35', TRUE, 'PRG-DP', '2025-26', NOW()),
('FSK2018223', 'Yashwant Deol', 'Grade 12', 'Equanimity', 'https://i.pravatar.cc/160?img=36', TRUE, 'PRG-DP', '2025-26', NOW()),
('FSK2025999', 'Inactive Demo Learner', 'Grade 10', 'G10', NULL, FALSE, 'PRG-MYP', '2025-26', NOW())
ON CONFLICT ("StudentCode") DO UPDATE SET
  "FullName" = EXCLUDED."FullName",
  "GradeName" = EXCLUDED."GradeName",
  "SectionName" = EXCLUDED."SectionName",
  "PhotoUrl" = EXCLUDED."PhotoUrl",
  "IsActive" = EXCLUDED."IsActive",
  "ProgramCode" = EXCLUDED."ProgramCode",
  "AcademicYearCode" = EXCLUDED."AcademicYearCode",
  "UpdatedAt" = NOW();

-- Staff: mix Academic / Admin / Support + IT Software + Nucleus-style system accounts
INSERT INTO "StaffDirectory" ("StaffCode", "FullName", "DepartmentName", "TeamName", "Designation", "PhotoUrl", "IsActive", "IsSystemAccount", "StaffCategory", "UpdatedAt")
VALUES
('STF-2001', 'Dr. Ananya Subramanian', 'Academics', 'Science HOD', 'HOD Science', 'https://i.pravatar.cc/120?img=41', TRUE, FALSE, 'Academic', NOW()),
('STF-2002', 'Ritesh Kothari', 'Academics', 'Mathematics', 'Senior Teacher', 'https://i.pravatar.cc/120?img=42', TRUE, FALSE, 'Academic', NOW()),
('STF-2003', 'Fatima Sheikh', 'Academics', 'Language & Literature', 'Teacher', 'https://i.pravatar.cc/120?img=43', TRUE, FALSE, 'Academic', NOW()),
('STF-2004', 'Gaurav Sinha', 'Administration', 'Admissions', 'Admissions Officer', 'https://i.pravatar.cc/120?img=44', TRUE, FALSE, 'Admin', NOW()),
('STF-2005', 'Pallavi Desai', 'Administration', 'HR Operations', 'HR Executive', 'https://i.pravatar.cc/120?img=45', TRUE, FALSE, 'Admin', NOW()),
('STF-2006', 'Sanjay Menon', 'Operations', 'Facilities', 'Facilities Lead', 'https://i.pravatar.cc/120?img=46', TRUE, FALSE, 'Support', NOW()),
('STF-2007', 'Elena D''Souza', 'Technology', 'IT Software', 'Product Analyst', 'https://i.pravatar.cc/120?img=47', TRUE, FALSE, 'Support', NOW()),
('STF-2008', 'Harsh Verma', 'Technology', 'IT Software', 'QA Engineer', 'https://i.pravatar.cc/120?img=48', TRUE, FALSE, 'Support', NOW()),
('STF-2009', 'SYSTEM-NUCLEUS-SSO', 'System', 'Nucleus', 'SSO Service', NULL, TRUE, TRUE, 'Support', NOW()),
('STF-2010', 'SYSTEM-NUCLEUS-API', 'System', 'Nucleus', 'API Integration', NULL, TRUE, TRUE, 'Support', NOW())
ON CONFLICT ("StaffCode") DO UPDATE SET
  "FullName" = EXCLUDED."FullName",
  "DepartmentName" = EXCLUDED."DepartmentName",
  "TeamName" = EXCLUDED."TeamName",
  "Designation" = EXCLUDED."Designation",
  "PhotoUrl" = EXCLUDED."PhotoUrl",
  "IsActive" = EXCLUDED."IsActive",
  "IsSystemAccount" = EXCLUDED."IsSystemAccount",
  "StaffCategory" = EXCLUDED."StaffCategory",
  "UpdatedAt" = NOW();

-- Ensure legacy IT rows stay on IT Software team (idempotent)
UPDATE "StaffDirectory" SET "TeamName" = 'IT Software', "DepartmentName" = 'Technology', "StaffCategory" = 'Support'
WHERE "StaffCode" IN ('STF-1007', 'STF-1008', 'STF-1101', 'STF-1102');

UPDATE "StaffDirectory" SET "StaffCategory" = 'Admin' WHERE "DepartmentName" = 'Administration';
UPDATE "StaffDirectory" SET "StaffCategory" = 'Support' WHERE "DepartmentName" IN ('Technology', 'Operations', 'System');
UPDATE "StaffDirectory" SET "StaffCategory" = 'Academic' WHERE "DepartmentName" = 'Academics';

-- HRT / mentor grid (grade + section from StudentDirectory-style naming)
INSERT INTO "GradeSectionMentor" ("GradeName", "SectionName", "MentorStaffCode", "MentorFullName", "UpdatedAt")
VALUES
('Grade 4', 'G4', 'STF-2002', 'Ritesh Kothari'),
('Grade 10', 'G10', 'STF-2001', 'Dr. Ananya Subramanian'),
('Grade 9', 'G9', 'STF-2003', 'Fatima Sheikh')
ON CONFLICT ("GradeName", "SectionName") DO UPDATE SET
  "MentorStaffCode" = EXCLUDED."MentorStaffCode",
  "MentorFullName" = EXCLUDED."MentorFullName",
  "UpdatedAt" = NOW();

-- HRIS-style leave balances (demo)
INSERT INTO "HrisLeaveBalance" ("StaffCode", "LeaveType", "BalanceDays", "AsOfDate", "UpdatedAt")
VALUES
('STF-2001', 'Annual', 22.0, CURRENT_DATE, NOW()),
('STF-2001', 'Casual', 4.5, CURRENT_DATE, NOW()),
('STF-2004', 'Annual', 16.0, CURRENT_DATE, NOW()),
('STF-2007', 'Annual', 10.5, CURRENT_DATE, NOW()),
('STF-2008', 'Sick', 8.0, CURRENT_DATE, NOW())
ON CONFLICT ("StaffCode", "LeaveType", "AsOfDate") DO UPDATE SET
  "BalanceDays" = EXCLUDED."BalanceDays",
  "UpdatedAt" = NOW();

-- Backfill programme for any legacy students still missing ProgramCode
UPDATE "StudentDirectory" SET "ProgramCode" = 'PRG-PYP' WHERE "ProgramCode" IS NULL AND "GradeName" ~ '^Grade [1-5]$';
UPDATE "StudentDirectory" SET "ProgramCode" = 'PRG-MYP' WHERE "ProgramCode" IS NULL AND "GradeName" ~ '^Grade (6|7|8|9|10)$';
UPDATE "StudentDirectory" SET "ProgramCode" = 'PRG-DP' WHERE "ProgramCode" IS NULL AND "GradeName" ~ '^Grade (11|12)$';
UPDATE "StudentDirectory" SET "AcademicYearCode" = '2025-26' WHERE "AcademicYearCode" IS NULL OR TRIM("AcademicYearCode") = '';
