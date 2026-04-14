using ApprovalDemo.Api.Configuration;
using ApprovalDemo.Api.Services;
using ApprovalDemo.SyncWorker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

if (DotEnvLoader.TryLoad(out var envPath))
{
    Console.WriteLine($"Loaded environment file: {envPath}");
}

// This process mirrors Supabase -> SQL Server; force sync on even if the shell copied Render env (false).
Environment.SetEnvironmentVariable("ENABLE_MSSQL_SYNC", "true");

var baseConfig = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

string postgres;
try
{
    postgres = DatabaseConnectionResolver.ResolvePostgresConnectionString(baseConfig);
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex.Message);
    return 1;
}

var mssql = DatabaseConnectionResolver.ResolveMssqlConnectionString(baseConfig);
if (string.IsNullOrWhiteSpace(mssql))
{
    Console.Error.WriteLine(
        "MSSQL_CONNECTION_STRING is missing. Add it to your environment or to ApprovalDemo/.env (see .env.example).");
    return 1;
}

var workerConfig = new ConfigurationBuilder()
    .AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["ConnectionStrings:DefaultConnection"] = postgres,
        ["ConnectionStrings:MssqlReporting"] = mssql,
    })
    .AddEnvironmentVariables()
    .Build();

using var services = new ServiceCollection()
    .AddSingleton<IConfiguration>(workerConfig)
    .AddLogging(b =>
    {
        b.AddSimpleConsole(o =>
        {
            o.SingleLine = true;
            o.TimestampFormat = "O ";
        });
        b.SetMinimumLevel(LogLevel.Information);
    })
    .AddSingleton<ApprovalSyncService>()
    .BuildServiceProvider();

var sync = services.GetRequiredService<ApprovalSyncService>();
await sync.InitializeAsync(CancellationToken.None);

var intervalMinutes = 1;
var intervalEnv = Environment.GetEnvironmentVariable("SYNC_INTERVAL_MINUTES");
if (!string.IsNullOrWhiteSpace(intervalEnv)
    && int.TryParse(intervalEnv, out var parsed)
    && parsed > 0)
{
    intervalMinutes = parsed;
}

Console.WriteLine($"Hybrid sync worker: Supabase -> SQL Server every {intervalMinutes} minute(s). Ctrl+C to stop.");

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

while (!cts.Token.IsCancellationRequested)
{
    try
    {
        var result = await sync.RunOnceAsync(cts.Token);
        Console.WriteLine(result.Enabled ? result.Message : $"Skipped: {result.Message}");
    }
    catch (OperationCanceledException) when (cts.Token.IsCancellationRequested)
    {
        break;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine(ex);
    }

    try
    {
        await Task.Delay(TimeSpan.FromMinutes(intervalMinutes), cts.Token);
    }
    catch (OperationCanceledException)
    {
        break;
    }
}

return 0;
