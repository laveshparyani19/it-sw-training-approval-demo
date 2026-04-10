-- Task 10: Staff directory (Supabase/PostgreSQL)
-- Run this in your Supabase database.

CREATE TABLE IF NOT EXISTS "StaffDirectory" (
    "Id" SERIAL PRIMARY KEY,
    "StaffCode" VARCHAR(50) NOT NULL UNIQUE,
    "FullName" VARCHAR(200) NOT NULL,
    "DepartmentName" VARCHAR(100) NOT NULL,
    "TeamName" VARCHAR(100) NOT NULL,
    "Designation" VARCHAR(120) NOT NULL,
    "PhotoUrl" TEXT NULL,
    "IsActive" BOOLEAN NOT NULL DEFAULT TRUE,
    "IsSystemAccount" BOOLEAN NOT NULL DEFAULT FALSE,
    "UpdatedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS "IX_StaffDirectory_Active_System_FullName"
    ON "StaffDirectory" ("IsActive", "IsSystemAccount", "FullName");

CREATE INDEX IF NOT EXISTS "IX_StaffDirectory_Department_Team"
    ON "StaffDirectory" ("DepartmentName", "TeamName");

INSERT INTO "StaffDirectory" ("StaffCode", "FullName", "DepartmentName", "TeamName", "Designation", "PhotoUrl", "IsActive", "IsSystemAccount")
VALUES
    ('STF-1001', 'Lavesh Paryani', 'Academics', 'Grade 7', 'Teacher', 'https://i.pravatar.cc/120?img=61', TRUE, FALSE),
    ('STF-1002', 'Lubaina Faizullabhai', 'Academics', 'Grade 6', 'Teacher', 'https://i.pravatar.cc/120?img=62', TRUE, FALSE),
    ('STF-1003', 'Lulua Bandookwala', 'Academics', 'Grade 5', 'Teacher', 'https://i.pravatar.cc/120?img=63', TRUE, FALSE),
    ('STF-1004', 'Maheen Khan', 'Academics', 'Math', 'Teacher', 'https://i.pravatar.cc/120?img=64', TRUE, FALSE),
    ('STF-1005', 'Mahek Kothari', 'Academics', 'Science', 'Teacher', 'https://i.pravatar.cc/120?img=65', TRUE, FALSE),
    ('STF-1006', 'Mahmood Yacoobali', 'Operations', 'Transport', 'Coordinator', 'https://i.pravatar.cc/120?img=66', TRUE, FALSE),
    ('STF-1007', 'Malhar Trivedi', 'Technology', 'IT', 'Administrator', 'https://i.pravatar.cc/120?img=67', TRUE, FALSE),
    ('STF-1008', 'Manish Tiwari', 'Technology', 'IT', 'Support Engineer', 'https://i.pravatar.cc/120?img=68', TRUE, FALSE),
    ('STF-1009', 'Manisha Guha', 'Academics', 'English', 'Teacher', 'https://i.pravatar.cc/120?img=69', TRUE, FALSE),
    ('STF-1010', 'Manisha Naik', 'Academics', 'Primary', 'Teacher', 'https://i.pravatar.cc/120?img=70', TRUE, FALSE),
    ('STF-1011', 'Mayank Kapadia', 'Administration', 'Office', 'Manager', 'https://i.pravatar.cc/120?img=71', TRUE, FALSE),
    ('STF-9999', 'SYSTEM ACCOUNT', 'System', 'Platform', 'Service User', NULL, TRUE, TRUE)
ON CONFLICT ("StaffCode") DO NOTHING;
