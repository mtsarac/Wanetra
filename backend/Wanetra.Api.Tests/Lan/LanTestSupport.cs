using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Wanetra.Api.Tests.Probes;
using Wanetra.Application.Lan;
using Wanetra.Application.Notifications;
using Wanetra.Domain;
using Wanetra.Infrastructure.Persistence;

namespace Wanetra.Api.Tests.Lan;

internal sealed class FakeLanScanner : ILanScanner
{
    public List<DiscoveredHost> Hosts { get; } = [];

    public string? Error { get; set; }

    public bool Throw { get; set; }

    public List<IReadOnlyList<CidrRange>> Scanned { get; } = [];

    public Task<LanScanResult> ScanAsync(IReadOnlyList<CidrRange> ranges, CancellationToken cancellationToken)
    {
        Scanned.Add(ranges);
        return Throw ? throw new InvalidOperationException("boom") : Task.FromResult(new LanScanResult([.. Hosts], Error));
    }
}

internal sealed class FakeNetworkDetector : ILanNetworkDetector
{
    public NetworkDetection Detection { get; set; } = new(
        [new DetectedNetwork(CidrRange.Of(System.Net.IPAddress.Parse("192.168.1.0"), 24), "eth0", "192.168.1.5")], false);

    public NetworkDetection Detect() => Detection;
}

internal sealed class FakeOui : IOuiLookup
{
    public string? Lookup(string mac) => mac.StartsWith("b8:27:eb", StringComparison.Ordinal) ? "Raspberry Pi Foundation" : null;
}

public sealed class LanApiFactory : WanetraApiFactory
{
    internal ManualTimeProvider Time { get; } = new(new DateTimeOffset(2026, 10, 18, 12, 0, 0, TimeSpan.Zero));

    internal FakeLanScanner Scanner { get; } = new();

    internal FakeNetworkDetector Detector { get; } = new();

    internal RecordingProvider Provider { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("Lan:Enabled", "true");
        builder.ConfigureServices(services =>
        {
            foreach (var descriptor in services
                .Where(service => service.ServiceType == typeof(IHostedService)
                    && service.ImplementationType?.Namespace?.StartsWith("Wanetra.Application", StringComparison.Ordinal) == true)
                .ToList())
            {
                services.Remove(descriptor);
            }

            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Time);
            services.RemoveAll<ILanScanner>();
            services.AddSingleton<ILanScanner>(Scanner);
            services.RemoveAll<ILanNetworkDetector>();
            services.AddSingleton<ILanNetworkDetector>(Detector);
            services.RemoveAll<IOuiLookup>();
            services.AddSingleton<IOuiLookup>(new FakeOui());
            services.RemoveAll<INotificationProvider>();
            services.AddSingleton<INotificationProvider>(Provider);
        });
    }

    internal static DiscoveredHost Host(string ip, string mac, string? hostname = null, string? description = null) =>
        new(ip, mac, hostname, description);

    internal async Task<LanScanOutcome> ScanAsync(bool advance = true)
    {
        LanScanOutcome outcome;
        await using (var scope = Services.CreateAsyncScope())
        {
            outcome = await scope.ServiceProvider.GetRequiredService<LanScanService>().RunAsync("manual", CancellationToken.None);
        }

        foreach (var transition in outcome.Transitions)
        {
            await using var scope = Services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<NotificationDispatcher>()
                .DispatchDeviceAlertAsync(transition.Trigger, transition.AlertId, CancellationToken.None);
        }

        if (advance)
        {
            Time.Advance(TimeSpan.FromMinutes(5));
        }

        return outcome;
    }

    internal async Task ScansAsync(int count)
    {
        for (var i = 0; i < count; i++)
        {
            await ScanAsync();
        }
    }

    internal async Task<T> WithScopeAsync<T>(Func<IServiceProvider, Task<T>> action)
    {
        await using var scope = Services.CreateAsyncScope();
        return await action(scope.ServiceProvider);
    }

    internal Task<List<Device>> DevicesAsync() =>
        WithScopeAsync(services => services.GetRequiredService<WanetraDbContext>().Devices.AsNoTracking().OrderBy(device => device.Id).ToListAsync());

    internal Task<List<DeviceEvent>> EventsAsync() =>
        WithScopeAsync(services => services.GetRequiredService<WanetraDbContext>().DeviceEvents.AsNoTracking().OrderBy(e => e.Id).ToListAsync());

    internal Task<List<DeviceAlert>> AlertsAsync() =>
        WithScopeAsync(services => services.GetRequiredService<WanetraDbContext>().DeviceAlerts
            .AsNoTracking().Include(alert => alert.NotificationDeliveries).OrderBy(alert => alert.Id).ToListAsync());

    internal Task<List<ProbeTarget>> ProbeTargetsAsync() =>
        WithScopeAsync(services => services.GetRequiredService<WanetraDbContext>().ProbeTargets.AsNoTracking().OrderBy(t => t.Id).ToListAsync());

    internal async Task AddWebhookDestinationAsync()
    {
        await WithScopeAsync(async services =>
        {
            var db = services.GetRequiredService<WanetraDbContext>();
            db.NotificationConfigurations.Add(new NotificationConfiguration
            {
                Provider = "webhook",
                Enabled = true,
                ConfigurationJson = "{}",
                CreatedAt = Time.GetUtcNow().UtcDateTime,
                UpdatedAt = Time.GetUtcNow().UtcDateTime,
            });
            await db.SaveChangesAsync();
            return 0;
        });
    }

    internal async Task SetAsync(long deviceId, Action<Device> change)
    {
        await WithScopeAsync(async services =>
        {
            var db = services.GetRequiredService<WanetraDbContext>();
            var device = await db.Devices.SingleAsync(d => d.Id == deviceId);
            change(device);
            await db.SaveChangesAsync();
            return 0;
        });
    }
}
