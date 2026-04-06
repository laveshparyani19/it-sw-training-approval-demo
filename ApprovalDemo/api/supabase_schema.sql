-- Supabase PostgreSQL schema for ApprovalDemo
-- Run in Supabase SQL editor.

CREATE EXTENSION IF NOT EXISTS pgcrypto;

CREATE TABLE IF NOT EXISTS "ApprovalRequest"
(
    "Id" serial PRIMARY KEY,
    "Title" text NOT NULL,
    "RequestedBy" text NOT NULL,
    "Status" smallint NOT NULL DEFAULT 0,
    "CreatedAt" timestamptz NOT NULL DEFAULT now(),
    "UpdatedAt" timestamptz NOT NULL DEFAULT now(),
    "IsDeleted" boolean NOT NULL DEFAULT false,
    "OperationId" uuid NOT NULL DEFAULT gen_random_uuid(),
    "DecisionBy" text NULL,
    "DecisionAt" timestamptz NULL,
    "RejectReason" text NULL
);

-- Keep schema compatible when table already exists.
ALTER TABLE "ApprovalRequest" ADD COLUMN IF NOT EXISTS "UpdatedAt" timestamptz;
ALTER TABLE "ApprovalRequest" ADD COLUMN IF NOT EXISTS "IsDeleted" boolean;
ALTER TABLE "ApprovalRequest" ADD COLUMN IF NOT EXISTS "OperationId" uuid;

UPDATE "ApprovalRequest" SET "UpdatedAt" = COALESCE("UpdatedAt", "CreatedAt", now());
UPDATE "ApprovalRequest" SET "IsDeleted" = COALESCE("IsDeleted", false);
UPDATE "ApprovalRequest" SET "OperationId" = COALESCE("OperationId", gen_random_uuid());

ALTER TABLE "ApprovalRequest" ALTER COLUMN "UpdatedAt" SET NOT NULL;
ALTER TABLE "ApprovalRequest" ALTER COLUMN "IsDeleted" SET NOT NULL;
ALTER TABLE "ApprovalRequest" ALTER COLUMN "OperationId" SET NOT NULL;

CREATE INDEX IF NOT EXISTS idx_approvalrequest_status ON "ApprovalRequest" ("Status");
CREATE INDEX IF NOT EXISTS idx_approvalrequest_updatedat ON "ApprovalRequest" ("UpdatedAt");

-- Sync state and dead-letter tables.
CREATE TABLE IF NOT EXISTS "SyncState"
(
    "Name" text PRIMARY KEY,
    "LastWatermark" timestamptz NOT NULL DEFAULT to_timestamp(0),
    "UpdatedAt" timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS "SyncDeadLetter"
(
    "Id" bigserial PRIMARY KEY,
    "SyncName" text NOT NULL,
    "EntityId" integer NOT NULL,
    "OperationId" uuid NOT NULL,
    "Payload" jsonb NOT NULL,
    "Error" text NOT NULL,
    "AttemptCount" integer NOT NULL,
    "CreatedAt" timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS "SyncReconciliationReport"
(
    "Id" bigserial PRIMARY KEY,
    "SyncName" text NOT NULL,
    "GeneratedAt" timestamptz NOT NULL,
    "SourceCount" integer NOT NULL,
    "TargetCount" integer NOT NULL,
    "MissingInTarget" integer NOT NULL,
    "MissingInSource" integer NOT NULL,
    "Summary" text NOT NULL
);

-- Optional: sample seed data (pending requests)
INSERT INTO "ApprovalRequest" ("Title", "RequestedBy", "Status", "CreatedAt", "UpdatedAt", "IsDeleted", "OperationId")
VALUES
('Vacation Request - John Doe', 'John Doe', 0, now() - interval '2 days', now() - interval '2 days', false, gen_random_uuid()),
('Hardware Purchase - Jane Smith', 'Jane Smith', 0, now() - interval '1 day 4 hours', now() - interval '1 day 4 hours', false, gen_random_uuid()),
('Conference Registration - Bob Wilson', 'Bob Wilson', 0, now() - interval '1 day', now() - interval '1 day', false, gen_random_uuid()),
('New Software License - Alice Brown', 'Alice Brown', 0, now() - interval '20 hours', now() - interval '20 hours', false, gen_random_uuid()),
('Office Chair Replacement', 'Daniel Lee', 0, now() - interval '18 hours', now() - interval '18 hours', false, gen_random_uuid()),
('Cloud Cost Increase Justification', 'Priya Nair', 0, now() - interval '16 hours', now() - interval '16 hours', false, gen_random_uuid()),
('Team Lunch Budget', 'Ravi Kumar', 0, now() - interval '14 hours', now() - interval '14 hours', false, gen_random_uuid()),
('VPN Access for Contractor', 'Sonia Patel', 0, now() - interval '12 hours', now() - interval '12 hours', false, gen_random_uuid()),
('Marketing Campaign Budget', 'Arjun Mehta', 0, now() - interval '10 hours', now() - interval '10 hours', false, gen_random_uuid()),
('Laptop Upgrade Request', 'Nikita Sharma', 0, now() - interval '9 hours', now() - interval '9 hours', false, gen_random_uuid()),
('Adobe Creative Cloud Renewal', 'Karan Singh', 0, now() - interval '8 hours', now() - interval '8 hours', false, gen_random_uuid()),
('Database Storage Expansion', 'Meera Iyer', 0, now() - interval '7 hours', now() - interval '7 hours', false, gen_random_uuid()),
('Customer Visit Travel Approval', 'Amit Verma', 0, now() - interval '6 hours', now() - interval '6 hours', false, gen_random_uuid()),
('Training Course Enrollment', 'Sneha Joshi', 0, now() - interval '5 hours', now() - interval '5 hours', false, gen_random_uuid()),
('Intern Stipend Disbursement', 'Rohit Das', 0, now() - interval '4 hours 30 minutes', now() - interval '4 hours 30 minutes', false, gen_random_uuid()),
('Security Audit Tool Purchase', 'Isha Kapoor', 0, now() - interval '4 hours', now() - interval '4 hours', false, gen_random_uuid()),
('Projector Repair Request', 'Vivek Rao', 0, now() - interval '3 hours 30 minutes', now() - interval '3 hours 30 minutes', false, gen_random_uuid()),
('Mobile Device Management License', 'Ananya Ghosh', 0, now() - interval '3 hours', now() - interval '3 hours', false, gen_random_uuid()),
('API Gateway Plan Upgrade', 'Harsh Jain', 0, now() - interval '2 hours 30 minutes', now() - interval '2 hours 30 minutes', false, gen_random_uuid()),
('Recruitment Drive Budget', 'Neha Malhotra', 0, now() - interval '2 hours', now() - interval '2 hours', false, gen_random_uuid()),
('Office Internet Plan Upgrade', 'Rahul Bhat', 0, now() - interval '90 minutes', now() - interval '90 minutes', false, gen_random_uuid()),
('Printer Cartridge Procurement', 'Pooja Arora', 0, now() - interval '75 minutes', now() - interval '75 minutes', false, gen_random_uuid()),
('Client Demo Event Expenses', 'Yash Khanna', 0, now() - interval '60 minutes', now() - interval '60 minutes', false, gen_random_uuid()),
('GitHub Copilot Team Subscription', 'Manav Sethi', 0, now() - interval '45 minutes', now() - interval '45 minutes', false, gen_random_uuid()),
('Ergonomic Keyboard Purchase', 'Tanya Chawla', 0, now() - interval '30 minutes', now() - interval '30 minutes', false, gen_random_uuid());
