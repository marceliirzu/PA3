using GpaCalculator.Data;
using GpaCalculator.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Override config from env vars at runtime
var claudeKey = Environment.GetEnvironmentVariable("CLAUDE_API_KEY");
if (!string.IsNullOrEmpty(claudeKey))
    builder.Configuration["ClaudeApiKey"] = claudeKey;

var connStr = Environment.GetEnvironmentVariable("MYSQL_CONNECTION_STRING");
if (!string.IsNullOrEmpty(connStr))
    builder.Configuration["ConnectionStrings:DefaultConnection"] = connStr;

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
        policy.WithOrigins(
            "https://marceliirzu.github.io",
            "http://localhost:5500",
            "http://127.0.0.1:5500"
        )
        .AllowAnyHeader()
        .AllowAnyMethod());
});

// Database
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? "";
if (!string.IsNullOrEmpty(connectionString))
{
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));
}
else
{
    // Use in-memory DB when no connection string is set (dev/test without MySQL)
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseInMemoryDatabase("GpaCalcDev"));
}

// App services
builder.Services.AddScoped<IClaudeService, ClaudeService>();
builder.Services.AddScoped<IRagService, RagService>();

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

// Ensure DB is created (non-fatal — app still starts if MySQL isn't ready yet)
try
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}
catch (Exception ex)
{
    app.Logger.LogWarning("DB EnsureCreated failed: {Message}", ex.Message);
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi(); // /openapi/v1.json
}

// CORS must come before routing/controllers
app.UseRouting();
app.UseCors("AllowFrontend");
app.UseAuthorization();

// Health check for Railway
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.MapControllers();

app.Run();
