using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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

        services.AddOptions<LibreSpeedOptions>()
            .Bind(configuration.GetSection(LibreSpeedOptions.SectionName))
            .Validate(options => options.TimeoutSeconds > 0, "SpeedTest:LibreSpeed:TimeoutSeconds must be greater than zero.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.ExecutablePath), "SpeedTest:LibreSpeed:ExecutablePath must be set.")
            .ValidateOnStart();

        services.AddSingleton<IProcessRunner, ProcessRunner>();
        services.AddScoped<ISpeedTestEngine, LibreSpeedEngine>();

        return services;
    }
}
