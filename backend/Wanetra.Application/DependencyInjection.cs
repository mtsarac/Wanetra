using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Wanetra.Application.SpeedTests;

namespace Wanetra.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<SpeedTestExecutor>();
        services.AddSingleton<SpeedTestCoordinator>();

        return services;
    }
}
