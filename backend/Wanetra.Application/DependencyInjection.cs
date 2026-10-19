using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Wanetra.Application.Alerts;
using Wanetra.Application.Auth;
using Wanetra.Application.Baselines;
using Wanetra.Application.Lan;
using Wanetra.Application.Notifications;
using Wanetra.Application.Probes;
using Wanetra.Application.Scheduling;
using Wanetra.Application.Settings;
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
        services.AddSingleton<SettingsSnapshot>();
        services.AddSingleton<SetupCodeProvider>();
        services.AddSingleton<LoginThrottle>();
        services.AddScoped<AuthService>();
        services.AddScoped<SettingsService>();
        services.AddScoped<SpeedTestExecutor>();
        services.AddScoped<BaselineService>();
        services.AddScoped<AlertEvaluationService>();
        services.AddScoped<NotificationDispatcher>();
        services.AddHostedService<NotificationDeliveryWorker>();
        services.AddScoped<NotificationConfigurationService>();
        services.AddScoped<INotificationProvider, NtfyNotificationProvider>();
        services.AddScoped<INotificationProvider, WebhookNotificationProvider>();
        services.AddSingleton<SpeedTestCoordinator>();
        services.AddSingleton<ScheduleCalculator>();
        services.AddSingleton<ScheduleChangeSignal>();
        services.AddScoped<ScheduleService>();
        services.AddHostedService<ScheduleWorker>();
        services.AddHostedService<DataRetentionWorker>();

        services.TryAddSingleton<IProbeMetrics, NoopProbeMetrics>();
        services.AddSingleton<ConnectivityState>();
        services.AddSingleton<OutageTracker>();
        services.AddScoped<ProbeTargetService>();
        services.AddScoped<ProbeCycleService>();
        services.AddScoped<OutageService>();
        services.AddScoped<ProbeRollupService>();
        services.AddScoped<ProbeQueryService>();
        services.AddHostedService<ProbeWorker>();
        services.AddHostedService<ProbeMaintenanceWorker>();

        services.TryAddSingleton<ILanMetrics, NoopLanMetrics>();
        services.AddSingleton<LanScanCoordinator>();
        services.AddScoped<DeviceProbeLinker>();
        services.AddScoped<LanScanService>();
        services.AddScoped<DeviceService>();
        services.AddHostedService<LanScanWorker>();

        return services;
    }
}
