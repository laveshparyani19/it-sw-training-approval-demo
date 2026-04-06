using ApprovalDemo.Api.Data;
using ApprovalDemo.Api.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Read Supabase password from environment variable and build connection string
var supabasePassword = Environment.GetEnvironmentVariable("SUPABASE_PASSWORD")
    ?? throw new InvalidOperationException("SUPABASE_PASSWORD environment variable not set");
var connectionString = $"Host=db.qxevtcviybjzqueipukf.supabase.co;Port=5432;Username=postgres;Password={supabasePassword};Database=postgres;SSL Mode=Require;Trust Server Certificate=true";
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

        policy.SetIsOriginAllowed(origin =>
        {
            // Always allow localhost for development
            if (origin.StartsWith("http://localhost") || origin.StartsWith("http://127.0.0.1"))
                return true;

            // If FRONTEND_URL is set in environment, allow it
            if (!string.IsNullOrEmpty(frontendUrl) && origin == frontendUrl)
                return true;

            // Allow any vercel.app subdomain for preview deployments
            if (origin.Contains(".vercel.app"))
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
        await context.Response.WriteAsJsonAsync(new { error = "Internal server error" });
    }
});

// CORS must be before other middleware
app.UseCors("AllowFrontend");
app.UseSecurityHeaders();

app.UseAuthorization();

app.MapControllers();

app.Run();
