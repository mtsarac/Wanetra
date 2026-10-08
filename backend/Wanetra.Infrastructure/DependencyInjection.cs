using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wanetra.Application.Notifications;
using Wanetra.Domain;
using Wanetra.Infrastructure.Persistence;
using Wanetra.Infrastructure.Processes;
using Wanetra.Infrastructure.SpeedTests;

namespace Wanetra.Infrastructure;

public static class DependencyInjection
{
    public const string DatabaseFileName = "wanetra.db";
    public const string EngineSettingKey = "SpeedTest:Engine";

    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
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
        services.AddHttpClient<NtfyNotificationProvider>();
        services.AddHttpClient<WebhookNotificationProvider>();

        services.AddSingleton<IProcessRunner, ProcessRunner>();

        var engine = (configuration[EngineSettingKey] ?? "librespeed").Trim().ToLowerInvariant();
        switch (engine)
        {
            case "librespeed":
                AddCliOptions<LibreSpeedOptions>(services, configuration, LibreSpeedOptions.SectionName, o => o.TimeoutSeconds)
                    .Validate(o => !string.IsNullOrWhiteSpace(o.ExecutablePath), $"{LibreSpeedOptions.SectionName}:ExecutablePath must be set.");
                services.AddScoped<ISpeedTestEngine, LibreSpeedEngine>();
                break;
            case "cloudflare":
                AddCliOptions<CloudflareOptions>(services, configuration, CloudflareOptions.SectionName, o => o.TimeoutSeconds)
                    .Validate(o => !string.IsNullOrWhiteSpace(o.ExecutablePath), $"{CloudflareOptions.SectionName}:ExecutablePath must be set.");
                services.AddScoped<ISpeedTestEngine, CloudflareSpeedEngine>();
                break;
            case "ookla":
                AddCliOptions<OoklaOptions>(services, configuration, OoklaOptions.SectionName, o => o.TimeoutSeconds)
                    .Validate(
                        o => o.AcceptLicense,
                        $"{OoklaOptions.SectionName}:AcceptLicense must be true to confirm you accepted the Ookla EULA and GDPR notice (https://www.speedtest.net/about/eula).");
                services.AddHttpClient<IOoklaBinary, OoklaBinaryInstaller>((httpClient, provider) => new OoklaBinaryInstaller(
                    httpClient,
                    Path.Combine(dataPath, "ookla"),
                    OoklaRelease.ForCurrentPlatform,
                    provider.GetRequiredService<ILogger<OoklaBinaryInstaller>>()))
                    // The archive is about 1 MB. Cap the wait so a host that drops packets reaches
                    // the ExecutablePath fallback quickly instead of after the 100 s default.
                    .ConfigureHttpClient(httpClient => httpClient.Timeout = TimeSpan.FromSeconds(30));
                services.AddScoped<ISpeedTestEngine, OoklaSpeedEngine>();
                break;
            default:
                throw new InvalidOperationException(
                    $"{EngineSettingKey} must be one of: librespeed, cloudflare, ookla (got '{engine}').");
        }

        return services;
    }

    private static OptionsBuilder<T> AddCliOptions<T>(
        IServiceCollection services,
        IConfiguration configuration,
        string sectionName,
        Func<T, int> timeoutSeconds)
        where T : class =>
        services.AddOptions<T>()
            .Bind(configuration.GetSection(sectionName))
            .Validate(options => timeoutSeconds(options) > 0, $"{sectionName}:TimeoutSeconds must be greater than zero.")
            .ValidateOnStart();
}
