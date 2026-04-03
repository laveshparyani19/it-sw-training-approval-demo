-- Supabase PostgreSQL schema for ApprovalDemo
-- Run in Supabase SQL editor:

CREATE TABLE IF NOT EXISTS "ApprovalRequest" (
    "Id" serial PRIMARY KEY,
    "Title" text NOT NULL,
    "RequestedBy" text NOT NULL,
    "Status" smallint NOT NULL DEFAULT 0,
    "CreatedAt" timestamptz NOT NULL DEFAULT now(),
    "DecisionBy" text NULL,
    "DecisionAt" timestamptz NULL,
    "RejectReason" text NULL
);

-- Optional: add indexes for status queries
CREATE INDEX IF NOT EXISTS idx_approvalrequest_status ON "ApprovalRequest" ("Status");
