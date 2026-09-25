using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Wanetra.Application.Alerts;
using Wanetra.Api.Endpoints;
using Prometheus;
using Wanetra.Api.Prometheus;
using Wanetra.Application;
using Wanetra.Infrastructure;
using Wanetra.Domain;
using Wanetra.Infrastructure.Persistence;
using Wanetra.Application.Maintenance;

var builder = WebApplication.CreateBuilder(args);

var dataPath = Path.GetFullPath(
    builder.Configuration["WANETRA_DATA_PATH"] ?? "/data",
    builder.Environment.ContentRootPath);
builder.Services.AddSingleton<IPrometheusMetrics, WanetraMetrics>();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration, dataPath);
builder.Services.AddOptions<DataRetentionOptions>()
    .Bind(builder.Configuration.GetSection(DataRetentionOptions.SectionName))
    .Validate(options => options.Days > 0, "DataRetention:Days must be greater than zero.")
    .ValidateOnStart();
builder.Services.AddHealthChecks()
    .AddDbContextCheck<WanetraDbContext>("database", tags: ["ready"]);

var app = builder.Build();

var metrics = app.Services.GetRequiredService<IPrometheusMetrics>();
await app.Services.InitializeDatabaseAsync();
await using (var scope = app.Services.CreateAsyncScope())
{
    var results = scope.ServiceProvider.GetRequiredService<ISpeedTestResultRepository>();
    var alerts = scope.ServiceProvider.GetRequiredService<IAlertStateRepository>();
    if (await alerts.GetRuleAsync(CancellationToken.None) is null)
    {
        var now = scope.ServiceProvider.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
        alerts.AddRule(new AlertRule
        {
            Name = "WAN health",
            Enabled = true,
            DownloadBaselineDropPercent = 30,
            ConsecutiveFailuresRequired = 3,
            ConsecutiveRecoveriesRequired = 2,
            CreatedAt = now,
            UpdatedAt = now,
        });
        await alerts.SaveChangesAsync(CancellationToken.None);
    }
    var alertEvaluationService = scope.ServiceProvider.GetRequiredService<AlertEvaluationService>();
    await alertEvaluationService.SynchronizeRuleStateAsync(
        await alerts.GetRuleAsync(CancellationToken.None),
        CancellationToken.None);

    metrics.Initialize(
        await results.FindLatestAsync(CancellationToken.None),
        await results.FindLatestSuccessfulAsync(CancellationToken.None),
        await alerts.GetOpenEventAsync(CancellationToken.None) is not null);
}

app.UseHttpMetrics();
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapHealthChecks("/health", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });
app.MapSettingsEndpoints();
app.MapMetrics("/metrics");
app.MapSpeedTestEndpoints();
app.MapScheduleEndpoints();
app.MapBaselineEndpoints();
app.MapAlertEndpoints();
app.MapNotificationEndpoints();
app.MapFallbackToFile("index.html");

app.Run();

public partial class Program;
