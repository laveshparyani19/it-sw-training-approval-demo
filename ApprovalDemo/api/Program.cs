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

// Enable CORS - whitelist specific domains
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(
            "https://it-sw-training-approval-demo-j346jc9qg.vercel.app",
            "https://it-sw-training-approval-demo.vercel.app",
            "http://localhost:4200",
            "http://localhost:3000")
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
    // Disable Swagger in production
    app.UseHsts(); // HTTP Strict Transport Security
}

app.UseSecurityHeaders();
app.UseHttpsRedirection();
app.UseCors("AllowFrontend");

app.UseAuthorization();

app.MapControllers();

app.Run();
