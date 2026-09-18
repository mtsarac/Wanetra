using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Wanetra.Api.Endpoints;
using Wanetra.Application;
using Wanetra.Infrastructure;
using Wanetra.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

var dataPath = Path.GetFullPath(
    builder.Configuration["WANETRA_DATA_PATH"] ?? "/data",
    builder.Environment.ContentRootPath);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration, dataPath);
builder.Services.AddHealthChecks()
    .AddDbContextCheck<WanetraDbContext>("database", tags: ["ready"]);

var app = builder.Build();

await app.Services.InitializeDatabaseAsync();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapHealthChecks("/health", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });
app.MapSpeedTestEndpoints();
app.MapScheduleEndpoints();
app.MapFallbackToFile("index.html");

app.Run();

public partial class Program;
