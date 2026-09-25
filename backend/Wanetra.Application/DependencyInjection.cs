using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Wanetra.Application.Alerts;
using Wanetra.Application.Baselines;
using Wanetra.Application.Notifications;
using Wanetra.Application.Scheduling;
using Wanetra.Application.SpeedTests;
using Wanetra.Application.Maintenance;
using Wanetra.Domain;

namespace Wanetra.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IPrometheusMetrics, NoopPrometheusMetrics>();
        services.AddScoped<SpeedTestExecutor>();
        services.AddScoped<BaselineService>();
        services.AddScoped<AlertEvaluationService>();
        services.AddScoped<NotificationDispatcher>();
        services.AddScoped<NotificationConfigurationService>();
        services.AddScoped<INotificationProvider, NtfyNotificationProvider>();
        services.AddScoped<INotificationProvider, WebhookNotificationProvider>();
        services.AddSingleton<SpeedTestCoordinator>();
        services.AddSingleton<ScheduleCalculator>();
        services.AddSingleton<ScheduleChangeSignal>();
        services.AddScoped<ScheduleService>();
        services.AddHostedService<ScheduleWorker>();
        services.AddHostedService<DataRetentionWorker>();

        return services;
    }
}
