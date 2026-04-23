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
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Ensure DB is created
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseStaticFiles();
app.UseCors("AllowFrontend");
app.UseAuthorization();

// Health check for Railway
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.MapControllers();

app.Run();
