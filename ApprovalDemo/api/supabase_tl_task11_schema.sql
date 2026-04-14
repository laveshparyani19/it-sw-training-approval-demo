-- Task 11: TL team + member assignments (Supabase / PostgreSQL)
-- Optional: tables are also created by the API on startup. Run this if you manage schema manually.

CREATE TABLE IF NOT EXISTS "TlTeamAssignment" (
    "Id" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "TlStaffCode" VARCHAR(50) NOT NULL,
    "DepartmentName" VARCHAR(100) NOT NULL,
    "TeamName" VARCHAR(100) NOT NULL,
    "MemberStaffIds" INTEGER[] NOT NULL,
    "TaskDescription" TEXT NULL,
    "CreatedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    "UpdatedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS "IX_TlTeamAssignment_TlStaffCode"
    ON "TlTeamAssignment" ("TlStaffCode");

CREATE INDEX IF NOT EXISTS "IX_TlTeamAssignment_Dept_Team"
    ON "TlTeamAssignment" ("DepartmentName", "TeamName");
