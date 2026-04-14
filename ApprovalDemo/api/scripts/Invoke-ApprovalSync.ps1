<#
.SYNOPSIS
  Triggers a Supabase -> SQL Server mirror sync via POST /api/sync/run.

.DESCRIPTION
  The API must already be running with a valid Supabase connection and (for sync)
  MSSQL_CONNECTION_STRING. Default URL matches Properties/launchSettings.json (http profile).

  Local workflow:
    1. Copy ..\.env.example to ..\.env and fill in real values (do not commit .env).
    2. In PowerShell from repo root or api folder:
         $env:SUPABASE_CONNECTION_STRING = '<from Supabase>'
         $env:MSSQL_CONNECTION_STRING = '<your SQL Server connection string>'
         $env:ENABLE_MSSQL_SYNC           = 'true'
       Then: dotnet run --project ApprovalDemo\api
    3. In another terminal: .\ApprovalDemo\api\scripts\Invoke-ApprovalSync.ps1

  Hybrid (Render API + local SQL): set ENABLE_MSSQL_SYNC=false on Render, then on your PC run:
      dotnet run --project ApprovalDemo/sync-worker
    (same SUPABASE_CONNECTION_STRING + local MSSQL_CONNECTION_STRING as env vars).

  Render with cloud SQL only: set MSSQL to an internet-reachable server, then POST:
      curl.exe -X POST "https://<your-api>/api/sync/run"

.PARAMETER BaseUrl
  Base URL of the running API (no trailing slash).

.PARAMETER Reconcile
  If set, calls POST /api/sync/reconcile instead of /api/sync/run.
#>
param(
    [string] $BaseUrl = "http://localhost:5249",
    [switch] $Reconcile
)

$path = if ($Reconcile) { "/api/sync/reconcile" } else { "/api/sync/run" }
$uri = "$BaseUrl$path".TrimEnd('/')

Write-Host "POST $uri"
try {
    $response = Invoke-RestMethod -Method Post -Uri $uri -ContentType "application/json"
    $response | ConvertTo-Json -Depth 6
} catch {
    Write-Error $_
    exit 1
}
