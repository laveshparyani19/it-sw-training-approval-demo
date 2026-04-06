using ApprovalDemo.Api.Data;

var builder = WebApplication.CreateBuilder(args);

// Read Supabase password from environment variable and build connection string
var supabasePassword = Environment.GetEnvironmentVariable("SUPABASE_PASSWORD") 
    ?? throw new InvalidOperationException("SUPABASE_PASSWORD environment variable not set");
var connectionString = $"Host=db.qxevtcviybjzqueipukf.supabase.co;Port=5432;Username=postgres;Password={supabasePassword};Database=postgres;SSL Mode=Require;Trust Server Certificate=true";
builder.Configuration["ConnectionStrings:DefaultConnection"] = connectionString;

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Register Repository
builder.Services.AddScoped<ApprovalRepository>();

// Enable CORS
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

app.UseCors("AllowFrontend");

app.UseAuthorization();

app.MapControllers();

app.Run();
