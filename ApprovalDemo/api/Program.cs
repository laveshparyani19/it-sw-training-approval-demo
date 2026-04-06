using ApprovalDemo.Api.Data;
using ApprovalDemo.Api.Middleware;
using System.Net;

var builder = WebApplication.CreateBuilder(args);

// Prefer a full connection string from env (pooler/direct), with backward-compatible fallback.
var rawConnectionString = Environment.GetEnvironmentVariable("SUPABASE_CONNECTION_STRING")
    ?? Environment.GetEnvironmentVariable("DATABASE_URL");

string connectionString;
if (!string.IsNullOrWhiteSpace(rawConnectionString))
{
    connectionString = rawConnectionString.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase)
        || rawConnectionString.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase)
        ? BuildNpgsqlConnectionStringFromUri(rawConnectionString)
        : rawConnectionString;
}
else
{
    var supabasePassword = Environment.GetEnvironmentVariable("SUPABASE_PASSWORD")
        ?? throw new InvalidOperationException("Set SUPABASE_CONNECTION_STRING (recommended) or SUPABASE_PASSWORD");
    var host = Environment.GetEnvironmentVariable("SUPABASE_HOST") ?? "db.qxevtcviybjzqueipukf.supabase.co";
    var port = Environment.GetEnvironmentVariable("SUPABASE_PORT") ?? "5432";
    var username = Environment.GetEnvironmentVariable("SUPABASE_USER") ?? "postgres";
    var database = Environment.GetEnvironmentVariable("SUPABASE_DB") ?? "postgres";

    connectionString = $"Host={host};Port={port};Username={username};Password={supabasePassword};Database={database};SSL Mode=Require;Trust Server Certificate=true";
}

builder.Configuration["ConnectionStrings:DefaultConnection"] = connectionString;

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

app.Run();

static string BuildNpgsqlConnectionStringFromUri(string uriString)
{
    var uri = new Uri(uriString);

    var userInfo = uri.UserInfo.Split(':', 2);
    if (userInfo.Length != 2)
    {
        throw new InvalidOperationException("Invalid Postgres URI format. Expected username and password in the URI.");
    }

    var username = WebUtility.UrlDecode(userInfo[0]);
    var password = WebUtility.UrlDecode(userInfo[1]);
    var database = uri.AbsolutePath.Trim('/');
    if (string.IsNullOrWhiteSpace(database))
    {
        database = "postgres";
    }

    return $"Host={uri.Host};Port={uri.Port};Username={username};Password={password};Database={database};SSL Mode=Require;Trust Server Certificate=true";
}
