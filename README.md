# IT SW Training Approval Demo

Full-stack training project implementing an approval workflow with Approve and Reject actions using ASP.NET Core API and Angular dashboard UI.

## Live URLs

- Frontend (Vercel): https://it-sw-training-approval-demo.vercel.app
- Backend API (Render): https://it-sw-training-approval-backend.onrender.com

## Training Task Mapping

Original task: Create form with approve and reject button in ASP.NET.

Implemented:
- Request listing form-style workflow with Approve and Reject actions
- ASP.NET API endpoints for create, approve, reject, and list operations
- Rejection reason modal and validation
- Dashboard UI with search and pagination

## Features

- Approve and reject actions with validation
- Pending request listing with modern dashboard layout
- Search and pagination on request table
- Supabase PostgreSQL as primary data store
- Optional MSSQL reporting mirror sync worker
- Manual sync and reconciliation endpoints

## Tech Stack

- Backend: .NET 10 ASP.NET Core Web API
- Frontend: Angular 21 (standalone components)
- Primary DB: Supabase PostgreSQL (Npgsql)
- Optional Reporting DB: MSSQL (Microsoft.Data.SqlClient)
- Hosting: Render (API), Vercel (UI)

## Repository About And Topics (GitHub)

Use this for GitHub About:

Training approval workflow dashboard with ASP.NET Core API and Angular UI, featuring approve/reject actions, search, pagination, and optional Supabase-to-MSSQL sync.

Suggested Topics:

- aspnet-core
- dotnet
- angular
- typescript
- webapi
- approval-workflow
- dashboard
- supabase
- postgresql
- mssql
- render
- vercel

## API Endpoints

- GET /api/approval-requests/pending
- GET /api/approval-requests/{id}
- POST /api/approval-requests
- POST /api/approval-requests/{id}/approve
- POST /api/approval-requests/{id}/reject
- POST /api/sync/run
- POST /api/sync/reconcile

## Local Development

Prerequisites:

- .NET 10 SDK
- Node.js 20+
- Supabase project (or PostgreSQL)
- Optional: SQL Server instance for reporting sync

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

## Database Setup

- Primary schema (Supabase/PostgreSQL): ApprovalDemo/api/supabase_schema.sql
- MSSQL mirror schema (optional): ApprovalDemo/api/mssql_reporting_schema.sql

## Environment Variables

Required for API:

- SUPABASE_CONNECTION_STRING

Optional for UI/API integration:

- FRONTEND_URL
- FRONTEND_URLS
- ALLOW_VERCEL_PREVIEWS
- VERCEL_PROJECT_SLUG

Optional for MSSQL sync:

- MSSQL_CONNECTION_STRING

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