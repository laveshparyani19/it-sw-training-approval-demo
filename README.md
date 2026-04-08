# IT SW Training Approval Demo

A production-style training project for approval workflows, built with ASP.NET Core Web API and Angular. The solution demonstrates request review operations, modern dashboard UX, and optional cross-database synchronization.

## Overview

This project was created as part of a corporate training assignment:

Create a form with Approve and Reject actions in ASP.NET.

Delivered implementation includes:

- End-to-end approve and reject flow with API validation
- Dashboard UI for request management
- Task 9 student selector with multi-select and profile cards
- Rejection reason capture and feedback notifications
- Search and pagination for operational usability
- Supabase as primary datastore with optional MSSQL reporting sync

## Live Environments

[![Frontend - Vercel](https://img.shields.io/badge/Frontend-Vercel-111111?style=for-the-badge&logo=vercel)](https://it-sw-training-approval-demo.vercel.app)
[![Backend API - Render](https://img.shields.io/badge/Backend%20API-Render-46E3B7?style=for-the-badge&logo=render&logoColor=0A0A0A)](https://it-sw-training-approval-backend.onrender.com)

## Key Features

- Approval request listing with action controls
- Approve and reject endpoints with business rule checks
- Search by ID, title, and requester
- Client-side pagination and page size controls
- Task switcher in dashboard (Task 2 and Task 9)
- Student directory API integration with:
  - Grade and section filtering
  - Name/code search
  - Paged loading for faster response
  - Multi-select with photo cards
- CORS handling for dynamic Vercel preview deployments
- Optional Supabase-to-MSSQL sync worker with:
  - Watermark-based incremental sync
  - Upsert semantics
  - Retry and dead-letter logging
  - Reconciliation reporting

## Technical Architecture

- Backend: .NET 10 ASP.NET Core Web API
- Frontend: Angular 21 (standalone components, SCSS)
- Primary Database: Supabase PostgreSQL (Npgsql)
- Optional Reporting Database: SQL Server (Microsoft.Data.SqlClient)
- Hosting: Render (API), Vercel (UI)

## API Surface

Approval operations:

- GET /api/approval-requests/pending
- GET /api/approval-requests/{id}
- POST /api/approval-requests
- POST /api/approval-requests/{id}/approve
- POST /api/approval-requests/{id}/reject

Sync operations:

- POST /api/sync/run
- POST /api/sync/reconcile

Student directory operations (Task 9):

- GET /api/students/grades
- GET /api/students/sections
- GET /api/students/directory
- GET /api/students/by-ids

## Local Setup

Prerequisites:

- .NET 10 SDK
- Node.js 20+
- Access to a Supabase project (or compatible PostgreSQL)
- Optional SQL Server instance for reporting sync tests

Backend:

```bash
cd ApprovalDemo/api
dotnet restore
dotnet run
```

Frontend:

```bash
cd ApprovalDemo/ui
npm install
npm start
```

## Database Setup Scripts

- Supabase schema and seed script:
  - ApprovalDemo/api/supabase_schema.sql
- Supabase Task 9 student directory schema:
  - ApprovalDemo/api/supabase_student_task9_schema.sql
- MSSQL reporting mirror schema:
  - ApprovalDemo/api/mssql_reporting_schema.sql
- MSSQL Task 9 student mirror schema:
  - ApprovalDemo/api/mssql_student_task9_schema.sql

## Environment Variables

Required:

- SUPABASE_CONNECTION_STRING

Optional UI/API integration:

- FRONTEND_URL
- FRONTEND_URLS
- ALLOW_VERCEL_PREVIEWS
- VERCEL_PROJECT_SLUG

Optional sync configuration:

- MSSQL_CONNECTION_STRING
- ENABLE_MSSQL_SYNC

Notes:

- ENABLE_MSSQL_SYNC defaults to disabled behavior unless set to true.
- Keep ENABLE_MSSQL_SYNC disabled on Render when MSSQL is intentionally unreachable to avoid repeated warning logs.

## Deployment Env Guide

Use different values per environment so sync only runs where MSSQL is actually reachable.

### Render flow (as shown in your Environment screen)

1. Open your Render service dashboard.
2. Go to Manage -> Environment.
3. Click Edit in the Environment Variables card.
4. Set or update values:
  - ENABLE_MSSQL_SYNC=false (recommended for Render unless SQL Server is network-reachable from Render)
  - MSSQL_CONNECTION_STRING=<set only when Render can reach SQL Server>
5. Save changes.
6. Trigger a deploy/redeploy so the new env values are loaded.

### Copy-paste env templates

Local machine (MSSQL reachable, sync ON):

```powershell
$env:ENABLE_MSSQL_SYNC="true"
$env:MSSQL_CONNECTION_STRING="Server=YOUR_SQL_HOST;Database=YOUR_DB;User Id=YOUR_USER;Password=YOUR_PASSWORD;Encrypt=True;TrustServerCertificate=True;"
$env:SUPABASE_CONNECTION_STRING="Host=YOUR_SUPABASE_HOST;Port=5432;Username=postgres;Password=YOUR_SUPABASE_PASSWORD;Database=postgres;SSL Mode=Require;Trust Server Certificate=true"
dotnet run
```

Staging (choose based on connectivity):

```env
ENABLE_MSSQL_SYNC=false
# Set only if staging can reach SQL Server:
# MSSQL_CONNECTION_STRING=Server=...;Database=...;User Id=...;Password=...;Encrypt=True;TrustServerCertificate=True;
SUPABASE_CONNECTION_STRING=Host=...;Port=5432;Username=postgres;Password=...;Database=postgres;SSL Mode=Require;Trust Server Certificate=true
```

Production on Render (typical safe default):

```env
ENABLE_MSSQL_SYNC=false
SUPABASE_CONNECTION_STRING=Host=...;Port=5432;Username=postgres;Password=...;Database=postgres;SSL Mode=Require;Trust Server Certificate=true
FRONTEND_URL=https://it-sw-training-approval-demo.vercel.app/
ALLOW_VERCEL_PREVIEWS=true
VERCEL_PROJECT_SLUG=it-sw-training-approval-demo
```

### Verification

- With sync OFF: POST /api/sync/run returns a disabled/skipped message.
- With sync ON and MSSQL reachable: POST /api/sync/run processes rows and returns success counts.
- If sync is ON but MSSQL is unreachable, switch ENABLE_MSSQL_SYNC back to false for that environment.

## Project Structure

```text
ApprovalDemo/
  api/
    Controllers/
    Data/
    Models/
    Services/
    Program.cs
    supabase_schema.sql
    mssql_reporting_schema.sql
  ui/
    src/
      app/
        components/
        models/
        services/
```

