using ApprovalDemo.Api.Configuration;
using ApprovalDemo.Api.Data;
using ApprovalDemo.Api.Services;
using ApprovalDemo.Api.Middleware;

var builder = WebApplication.CreateBuilder(args);

var connectionString = DatabaseConnectionResolver.ResolvePostgresConnectionString(builder.Configuration);
builder.Configuration["ConnectionStrings:DefaultConnection"] = connectionString;

var mssqlConnectionString = DatabaseConnectionResolver.ResolveMssqlConnectionString(builder.Configuration);
if (!string.IsNullOrWhiteSpace(mssqlConnectionString))
{
    builder.Configuration["ConnectionStrings:MssqlReporting"] = mssqlConnectionString;
}

// Configure Kestrel with request size limits
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 1024 * 100; // 100 KB max
});

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Register Repository
builder.Services.AddScoped<ApprovalRepository>();
builder.Services.AddScoped<StudentRepository>();
builder.Services.AddScoped<StaffRepository>();
builder.Services.AddScoped<TlTeamAssignmentRepository>();
builder.Services.AddSingleton<ApprovalSyncService>();
var enableMssqlSync = !string.Equals(
    Environment.GetEnvironmentVariable("ENABLE_MSSQL_SYNC") ?? builder.Configuration["ENABLE_MSSQL_SYNC"],
    "false",
    StringComparison.OrdinalIgnoreCase)
    && !string.IsNullOrWhiteSpace(mssqlConnectionString);
if (enableMssqlSync)
{
    builder.Services.AddHostedService<ApprovalSyncHostedService>();
}

// Enable CORS - allow dynamic frontend URLs
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        var frontendUrl = Environment.GetEnvironmentVariable("FRONTEND_URL");
        var frontendUrls = Environment.GetEnvironmentVariable("FRONTEND_URLS");
        var allowVercelPreviews =
            !string.Equals(Environment.GetEnvironmentVariable("ALLOW_VERCEL_PREVIEWS"), "false", StringComparison.OrdinalIgnoreCase);
        var vercelProjectSlug = Environment.GetEnvironmentVariable("VERCEL_PROJECT_SLUG");

        var explicitAllowedOrigins = new List<string>();
        if (!string.IsNullOrWhiteSpace(frontendUrl))
        {
            explicitAllowedOrigins.Add(frontendUrl.Trim().TrimEnd('/'));
        }

        if (!string.IsNullOrWhiteSpace(frontendUrls))
        {
            explicitAllowedOrigins.AddRange(
                frontendUrls
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(o => o.TrimEnd('/')));
        }

        policy.SetIsOriginAllowed(origin =>
        {
            if (!Uri.TryCreate(origin, UriKind.Absolute, out var originUri))
                return false;

            var host = originUri.Host;

            // Always allow localhost for development
            if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase) || host.Equals("127.0.0.1"))
                return true;

            // Allow one or many explicit origins from env vars.
            if (explicitAllowedOrigins.Contains(origin.TrimEnd('/'), StringComparer.OrdinalIgnoreCase))
                return true;

            // Allow Vercel preview domains.
            if (allowVercelPreviews && host.EndsWith(".vercel.app", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(vercelProjectSlug))
                    return true;

                var projectRoot = $"{vercelProjectSlug}.vercel.app";
                if (host.Equals(projectRoot, StringComparison.OrdinalIgnoreCase))
                    return true;

                if (host.StartsWith($"{vercelProjectSlug}-", StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            // Optionally allow custom vercel domains (e.g., *.vercel.app.cn or edge cases)
            if (allowVercelPreviews && host.EndsWith("vercel.app", StringComparison.OrdinalIgnoreCase))
                return true;

            return false;
        })
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseHsts(); // HTTP Strict Transport Security
}

app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (Exception ex)
    {
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Unhandled exception while processing request");

        context.Response.Clear();
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        if (context.Request.Headers.TryGetValue("Origin", out var origin))
        {
            context.Response.Headers["Access-Control-Allow-Origin"] = origin.ToString();
            context.Response.Headers["Access-Control-Allow-Credentials"] = "true";
        }
        await context.Response.WriteAsJsonAsync(new
        {
            error = "Internal server error",
            message = ex.Message,
            stackTrace = ex.StackTrace,
            detail = ex.InnerException?.Message
        });
    }
});

// CORS must be before other middleware
app.UseCors("AllowFrontend");
app.UseSecurityHeaders();

app.UseAuthorization();

app.MapControllers();

using (var scope = app.Services.CreateScope())
{
    var studentRepository = scope.ServiceProvider.GetRequiredService<StudentRepository>();
    var staffRepository = scope.ServiceProvider.GetRequiredService<StaffRepository>();
    var tlTeamAssignmentRepository = scope.ServiceProvider.GetRequiredService<TlTeamAssignmentRepository>();
    await studentRepository.EnsureSchemaAndSeedAsync(CancellationToken.None);
    await staffRepository.EnsureSchemaAndSeedAsync(CancellationToken.None);
    await tlTeamAssignmentRepository.EnsureSchemaAsync(CancellationToken.None);

    if (enableMssqlSync)
    {
        var syncService = scope.ServiceProvider.GetRequiredService<ApprovalSyncService>();
        await syncService.InitializeAsync(CancellationToken.None);
    }
}

app.Run();
