-- Task 11: Sample rows for TlTeamAssignment (Supabase / PostgreSQL)
-- Prerequisites: "StaffDirectory" is populated (e.g. Task 10 seed).
-- Run in the Supabase SQL Editor. MSSQL mirror will pick these up on the next sync when enabled.

-- Optional: remove previous demo rows for these TLs before re-running
-- DELETE FROM "TlTeamAssignment" WHERE "TlStaffCode" IN ('STF-1007', 'STF-1011', 'STF-1001');

INSERT INTO "TlTeamAssignment" (
    "TlStaffCode",
    "DepartmentName",
    "TeamName",
    "MemberStaffIds",
    "TaskDescription"
)
VALUES
(
    'STF-1007',
    'Technology',
    'IT',
    ARRAY(
        SELECT s."Id"
        FROM "StaffDirectory" AS s
        WHERE s."DepartmentName" = 'Technology'
          AND s."TeamName" = 'IT'
          AND COALESCE(s."IsSystemAccount", FALSE) = FALSE
        ORDER BY s."StaffCode"
    ),
    'Patch critical servers, verify backup jobs, and document changes for the IT team.'
),
(
    'STF-1011',
    'Administration',
    'Office',
    ARRAY(
        SELECT s."Id"
        FROM "StaffDirectory" AS s
        WHERE s."StaffCode" = 'STF-1011'
        LIMIT 1
    ),
    'Prepare board report and fee reconciliation summary for the office.'
),
(
    'STF-1001',
    'Academics',
    'Grade 7',
    ARRAY(
        SELECT s."Id"
        FROM "StaffDirectory" AS s
        WHERE s."StaffCode" = 'STF-1001'
        LIMIT 1
    ),
    'Coordinate Grade 7 assessments and draft parent communication.'
);
