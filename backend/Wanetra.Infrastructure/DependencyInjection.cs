using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wanetra.Application.Notifications;
using Wanetra.Application.Settings;
using Wanetra.Domain;
using Wanetra.Infrastructure.Persistence;
using Wanetra.Infrastructure.Processes;
using Wanetra.Infrastructure.SpeedTests;

namespace Wanetra.Infrastructure;

public static class DependencyInjection
{
    public const string DatabaseFileName = "wanetra.db";

    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        string dataPath)
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = Path.Combine(dataPath, DatabaseFileName),
        }.ToString();

        services.AddDbContext<WanetraDbContext>(options => options.UseSqlite(connectionString));
        services.AddScoped<ISpeedTestResultRepository, SpeedTestResultRepository>();
        services.AddScoped<IScheduleSettingsRepository, ScheduleSettingsRepository>();
        services.AddScoped<IAlertStateRepository, AlertStateRepository>();
        services.AddScoped<INotificationConfigurationRepository, NotificationConfigurationRepository>();
        services.AddScoped<IAppSettingRepository, AppSettingRepository>();
        services.AddHttpClient<NtfyNotificationProvider>();
        services.AddHttpClient<WebhookNotificationProvider>();

        services.AddSingleton<IProcessRunner, ProcessRunner>();

        // Always register the Ookla installer typed client (30 s timeout)
        services.AddHttpClient<IOoklaBinary, OoklaBinaryInstaller>((httpClient, provider) => new OoklaBinaryInstaller(
            httpClient,
            Path.Combine(dataPath, "ookla"),
            OoklaRelease.ForCurrentPlatform,
            provider.GetRequiredService<ILogger<OoklaBinaryInstaller>>()))
            .ConfigureHttpClient(httpClient => httpClient.Timeout = TimeSpan.FromSeconds(30));

        // Register typed options built from snapshot
        services.AddScoped<IOptions<LibreSpeedOptions>>(sp =>
        {
            var snapshot = sp.GetRequiredService<SettingsSnapshot>();
            return Options.Create(new LibreSpeedOptions
            {
                TimeoutSeconds = snapshot.GetInt(SettingKeys.SpeedTestLibreSpeedTimeoutSeconds),
                ServerId = snapshot.GetOptionalInt(SettingKeys.SpeedTestLibreSpeedServerId),
                ExecutablePath = snapshot.GetString(SettingKeys.SpeedTestLibreSpeedExecutablePath),
            });
        });

        services.AddScoped<IOptions<CloudflareOptions>>(sp =>
        {
            var snapshot = sp.GetRequiredService<SettingsSnapshot>();
            return Options.Create(new CloudflareOptions
            {
                TimeoutSeconds = snapshot.GetInt(SettingKeys.SpeedTestCloudflareTimeoutSeconds),
                ExecutablePath = snapshot.GetString(SettingKeys.SpeedTestCloudflareExecutablePath),
            });
        });

        services.AddScoped<IOptions<OoklaOptions>>(sp =>
        {
            var snapshot = sp.GetRequiredService<SettingsSnapshot>();
            return Options.Create(new OoklaOptions
            {
                TimeoutSeconds = snapshot.GetInt(SettingKeys.SpeedTestOoklaTimeoutSeconds),
                ServerId = snapshot.GetOptionalInt(SettingKeys.SpeedTestOoklaServerId),
                ExecutablePath = snapshot.GetOptionalString(SettingKeys.SpeedTestOoklaExecutablePath),
                AcceptLicense = snapshot.GetBool(SettingKeys.SpeedTestOoklaAcceptLicense),
            });
        });

        // Register all three engines scoped
        services.AddScoped<LibreSpeedEngine>();
        services.AddScoped<CloudflareSpeedEngine>();
        services.AddScoped<OoklaSpeedEngine>();

        // Register ISpeedTestEngine factory picking the engine named by the snapshot
        services.AddScoped<ISpeedTestEngine>(sp =>
        {
            var snapshot = sp.GetRequiredService<SettingsSnapshot>();
            return snapshot.SpeedTestEngine.ToLowerInvariant() switch
            {
                SpeedTestEngineNames.LibreSpeed => sp.GetRequiredService<LibreSpeedEngine>(),
                SpeedTestEngineNames.Cloudflare => sp.GetRequiredService<CloudflareSpeedEngine>(),
                SpeedTestEngineNames.Ookla => sp.GetRequiredService<OoklaSpeedEngine>(),
                var unknown => throw new InvalidOperationException($"Unknown speed test engine '{unknown}'."),
            };
        });

        return services;
    }

    // Keep backwards compatible overload for callers passing (configuration, dataPath)
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        string dataPath) =>
        services.AddInfrastructure(dataPath);
}
