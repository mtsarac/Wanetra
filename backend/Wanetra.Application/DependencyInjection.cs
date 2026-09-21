using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Wanetra.Application.Alerts;
using Wanetra.Application.Baselines;
using Wanetra.Application.Scheduling;
using Wanetra.Application.SpeedTests;

namespace Wanetra.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<SpeedTestExecutor>();
        services.AddScoped<BaselineService>();
        services.AddScoped<AlertEvaluationService>();
        services.AddSingleton<SpeedTestCoordinator>();
        services.AddSingleton<ScheduleCalculator>();
        services.AddSingleton<ScheduleChangeSignal>();
        services.AddScoped<ScheduleService>();
        services.AddHostedService<ScheduleWorker>();

        return services;
    }
}
