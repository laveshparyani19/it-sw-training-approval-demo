-- Task 9: Student directory (Supabase/PostgreSQL)
-- Run this in your own Supabase database.

CREATE TABLE IF NOT EXISTS "StudentDirectory" (
    "Id" SERIAL PRIMARY KEY,
    "StudentCode" VARCHAR(50) NOT NULL UNIQUE,
    "FullName" VARCHAR(200) NOT NULL,
    "GradeName" VARCHAR(50) NOT NULL,
    "SectionName" VARCHAR(50) NOT NULL,
    "PhotoUrl" TEXT NULL,
    "IsActive" BOOLEAN NOT NULL DEFAULT TRUE,
    "UpdatedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS "IX_StudentDirectory_Grade_Section"
    ON "StudentDirectory" ("GradeName", "SectionName");

CREATE INDEX IF NOT EXISTS "IX_StudentDirectory_Active_FullName"
    ON "StudentDirectory" ("IsActive", "FullName");

INSERT INTO "StudentDirectory" ("StudentCode", "FullName", "GradeName", "SectionName", "PhotoUrl", "IsActive")
VALUES
    ('S-1001', 'Aarav Shah', 'Grade 5', 'A', 'https://i.pravatar.cc/160?img=11', TRUE),
    ('S-1002', 'Mira Patel', 'Grade 5', 'A', 'https://i.pravatar.cc/160?img=32', TRUE),
    ('S-1003', 'Vihaan Desai', 'Grade 5', 'B', 'https://i.pravatar.cc/160?img=14', TRUE),
    ('S-1004', 'Ira Mehta', 'Grade 6', 'A', 'https://i.pravatar.cc/160?img=47', TRUE),
    ('S-1005', 'Kabir Trivedi', 'Grade 6', 'B', 'https://i.pravatar.cc/160?img=52', TRUE),
    ('S-1006', 'Riya Joshi', 'Grade 7', 'A', 'https://i.pravatar.cc/160?img=5', TRUE),
    ('S-1007', 'Aryan Modi', 'Grade 7', 'C', NULL, TRUE),
    ('S-1008', 'Diya Nair', 'Grade 8', 'B', 'https://i.pravatar.cc/160?img=44', TRUE)
ON CONFLICT ("StudentCode") DO NOTHING;
