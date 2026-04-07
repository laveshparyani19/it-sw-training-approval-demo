# IT SW Training Approval Demo

A production-style training project for approval workflows, built with ASP.NET Core Web API and Angular. The solution demonstrates request review operations, modern dashboard UX, and optional cross-database synchronization.

## Overview

This project was created as part of a corporate training assignment:

Create a form with Approve and Reject actions in ASP.NET.

Delivered implementation includes:

- End-to-end approve and reject flow with API validation
- Dashboard UI for request management
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
- MSSQL reporting mirror schema:
  - ApprovalDemo/api/mssql_reporting_schema.sql

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

