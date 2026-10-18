using System.Collections.Concurrent;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Wanetra.Application.Notifications;
using Wanetra.Application.Probes;
using Wanetra.Application.SpeedTests;
using Wanetra.Api.Tests.SpeedTests;
using Wanetra.Domain;
using Wanetra.Infrastructure.Persistence;

namespace Wanetra.Api.Tests.Probes;

internal sealed class FakeProbeExecutor : IProbeExecutor
{
    public ConcurrentQueue<ProbeRequest> Requests { get; } = new();

    public Func<ProbeRequest, ProbeOutcome> Behavior { get; set; } = _ => ProbeOutcome.Up(12);

    public void EverythingUp() => Behavior = _ => ProbeOutcome.Up(12);

    public void EverythingDownExcept(params string[] hosts) =>
        Behavior = request => hosts.Contains(request.Host) ? ProbeOutcome.Up(12) : ProbeOutcome.Down("timeout");

    public void Failing(Func<ProbeRequest, bool> failing) =>
        Behavior = request => failing(request) ? ProbeOutcome.Down("timeout") : ProbeOutcome.Up(12);

    public Task<ProbeOutcome> ExecuteAsync(ProbeRequest request, CancellationToken cancellationToken)
    {
        Requests.Enqueue(request);
        return Task.FromResult(Behavior(request));
    }
}

internal sealed class FakeGatewayDetector : IGatewayDetector
{
    public string? Address { get; set; } = "192.168.1.1";

    public string? DetectGateway() => Address;
}

internal sealed class RecordingProvider : INotificationProvider
{
    public string Name => "webhook";

    public bool Failing { get; set; }

    public List<NotificationMessage> Sent { get; } = [];

    public Task SendAsync(NotificationMessage message, string configurationJson, CancellationToken cancellationToken)
    {
        if (Failing)
        {
            throw new HttpRequestException("offline");
        }

        Sent.Add(message);
        return Task.CompletedTask;
    }
}

public sealed class ProbeApiFactory : WanetraApiFactory
{
    internal const string Gateway = "192.168.1.1";

    internal ManualTimeProvider Time { get; } = new(new DateTimeOffset(2026, 10, 18, 12, 0, 0, TimeSpan.Zero));

    internal FakeProbeExecutor Executor { get; } = new();

    internal FakeGatewayDetector GatewayDetector { get; } = new();

    internal RecordingProvider Provider { get; } = new();

    internal TaskCompletionSource ReleaseSpeedTest { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        // Safe to enable: the hosted loops are removed below, so nothing probes on its own.
        builder.UseSetting("Probes:Enabled", "true");
        builder.ConfigureServices(services =>
        {
            // Background loops would race the tests; each test drives the services itself.
            foreach (var descriptor in services
                .Where(service => service.ServiceType == typeof(IHostedService)
                    && service.ImplementationType?.Namespace?.StartsWith("Wanetra.Application", StringComparison.Ordinal) == true)
                .ToList())
            {
                services.Remove(descriptor);
            }

            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Time);
            services.RemoveAll<IProbeExecutor>();
            services.AddSingleton<IProbeExecutor>(Executor);
            services.RemoveAll<IGatewayDetector>();
            services.AddSingleton<IGatewayDetector>(GatewayDetector);
            services.RemoveAll<INotificationProvider>();
            services.AddSingleton<INotificationProvider>(Provider);
            services.RemoveAll<ISpeedTestEngine>();
            services.AddScoped<ISpeedTestEngine>(_ => new StubSpeedTestEngine(async token =>
            {
                await ReleaseSpeedTest.Task.WaitAsync(token);
                return new SpeedTestResult { Engine = "stub", Success = true, DownloadMbps = 100, UploadMbps = 20, LatencyMs = 10 };
            }));
        });
    }

    internal async Task<ProbeCycleResult> CycleAsync(bool advance = true)
    {
        ProbeCycleResult result;
        await using (var scope = Services.CreateAsyncScope())
        {
            result = await scope.ServiceProvider.GetRequiredService<ProbeCycleService>().RunAsync(CancellationToken.None);
        }

        if (result.Transition is { } transition)
        {
            await using var scope = Services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<NotificationDispatcher>()
                .DispatchOutageAsync(transition.Trigger, transition.OutageId, CancellationToken.None);
        }

        if (advance)
        {
            Time.Advance(TimeSpan.FromSeconds(10));
        }

        return result;
    }

    internal async Task CyclesAsync(int count)
    {
        for (var i = 0; i < count; i++)
        {
            await CycleAsync();
        }
    }

    internal async Task WithDbAsync(Func<WanetraDbContext, Task> action)
    {
        await using var scope = Services.CreateAsyncScope();
        await action(scope.ServiceProvider.GetRequiredService<WanetraDbContext>());
    }

    internal async Task<T> WithScopeAsync<T>(Func<IServiceProvider, Task<T>> action)
    {
        await using var scope = Services.CreateAsyncScope();
        return await action(scope.ServiceProvider);
    }

    internal async Task AddWebhookDestinationAsync()
    {
        await WithDbAsync(async db =>
        {
            db.NotificationConfigurations.Add(new NotificationConfiguration
            {
                Provider = "webhook",
                Enabled = true,
                ConfigurationJson = "{}",
                CreatedAt = Time.GetUtcNow().UtcDateTime,
                UpdatedAt = Time.GetUtcNow().UtcDateTime,
            });
            await db.SaveChangesAsync();
        });
    }

    internal Task<Outage?> LatestOutageAsync() =>
        WithScopeAsync(services => services.GetRequiredService<WanetraDbContext>().Outages
            .Include(outage => outage.NotificationDeliveries)
            .OrderByDescending(outage => outage.Id)
            .FirstOrDefaultAsync());
}
