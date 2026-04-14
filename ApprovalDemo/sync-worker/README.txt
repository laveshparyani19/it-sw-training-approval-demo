Hybrid database sync (Supabase -> SQL Server)
==============================================

Use this when the API runs on Render without MSSQL (or with ENABLE_MSSQL_SYNC=false)
and SQL Server lives on your PC or private network.

1. On Render: set ENABLE_MSSQL_SYNC=false OR remove MSSQL_CONNECTION_STRING so the
 cloud API does not try to connect to local SQL.

2. On your machine, set the same Supabase connection string you use in production,
   plus your local SQL connection string:

   SUPABASE_CONNECTION_STRING=... (or DATABASE_URL / SUPABASE_PASSWORD combo)
   MSSQL_CONNECTION_STRING=Server=...;Database=ApprovalDemoDb;...
   ENABLE_MSSQL_SYNC=true

 Optional: SYNC_INTERVAL_MINUTES=1 (default 1)

3. Run from repo root (or any folder under the repo):

   dotnet run --project ApprovalDemo/sync-worker

     The worker auto-loads the first file found among:
   - ./.env
   - ./ApprovalDemo/.env
   - parents of the build output (so ApprovalDemo/.env works when you run from IT SW Training).

   Each line can be standard "NAME=value" or PowerShell-style:
   $env:NAME = 'value'

The worker uses the same ApprovalSyncService as the web app: approvals watermark,
StudentDirectory, StaffDirectory, TlTeamAssignment, and mirror deletes.
