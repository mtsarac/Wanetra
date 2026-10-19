using System.Net.Http.Json;
using Wanetra.Domain;
using static Wanetra.Api.Tests.Lan.LanApiFactory;

namespace Wanetra.Api.Tests.Lan;

public class LanScanServiceTests
{
    private const string Router = "b8:27:eb:00:00:01";
    private const string Laptop = "aa:bb:cc:00:00:02";
    private const string Phone = "da:a1:19:00:00:03";
    private const string Camera = "aa:bb:cc:00:00:04";

    private static async Task SeedBaselineAsync(LanApiFactory factory)
    {
        factory.Scanner.Hosts.AddRange(
        [
            Host("192.168.1.1", Router, "router", "Home router"),
            Host("192.168.1.20", Laptop, "ali-laptop"),
            Host("192.168.1.30", Phone),
        ]);
        await factory.ScanAsync();
    }

    [Fact]
    public async Task The_first_scan_learns_the_network_without_raising_alerts()
    {
        await using var factory = new LanApiFactory();
        _ = factory.CreateClient();
        await factory.AddWebhookDestinationAsync();

        await SeedBaselineAsync(factory);

        var devices = await factory.DevicesAsync();
        Assert.Equal(3, devices.Count);
        Assert.All(devices, device =>
        {
            Assert.True(device.Known);
            Assert.True(device.Online);
        });
        Assert.Empty(await factory.EventsAsync());
        Assert.Empty(await factory.AlertsAsync());
        Assert.Empty(factory.Provider.Sent);

        var router = devices.Single(device => device.Mac == Router);
        Assert.Equal("Raspberry Pi Foundation", router.Vendor);
        Assert.Equal("router", router.Hostname);
        Assert.Equal("Home router", router.Description);
        Assert.False(router.RandomizedMac);

        var phone = devices.Single(device => device.Mac == Phone);
        Assert.True(phone.RandomizedMac);
        Assert.Null(phone.Vendor);
    }

    [Fact]
    public async Task A_new_device_after_the_baseline_is_unknown_and_raises_one_alert()
    {
        await using var factory = new LanApiFactory();
        _ = factory.CreateClient();
        await factory.AddWebhookDestinationAsync();
        await SeedBaselineAsync(factory);

        factory.Scanner.Hosts.Add(Host("192.168.1.40", Camera, "cam-7"));
        await factory.ScanAsync();
        await factory.ScanAsync();

        var camera = (await factory.DevicesAsync()).Single(device => device.Mac == Camera);
        Assert.False(camera.Known);
        var joined = Assert.Single(await factory.EventsAsync());
        Assert.Equal(DeviceEventType.Joined, joined.Type);
        Assert.Equal(camera.Id, joined.DeviceId);
        var alert = Assert.Single(await factory.AlertsAsync());
        Assert.Equal(DeviceAlertKind.NewDevice, alert.Kind);
        Assert.Equal("cam-7", alert.DeviceLabel);
        Assert.True(alert.NotificationSent);

        var message = Assert.Single(factory.Provider.Sent);
        Assert.Equal("device.new", message.Event);
        Assert.Contains("cam-7 joined the network", message.Body);
        Assert.Contains(Camera, message.Body);
        Assert.Contains("192.168.1.40", message.Body);
    }

    [Fact]
    public async Task New_device_alerts_can_be_turned_off()
    {
        await using var factory = new LanApiFactory();
        var client = factory.CreateClient();
        await factory.AddWebhookDestinationAsync();
        await SeedBaselineAsync(factory);
        var put = await client.PutAsJsonAsync("/api/settings", new { values = new Dictionary<string, object?> { ["lan.alertOnNewDevice"] = false } });
        Assert.True(put.IsSuccessStatusCode);

        factory.Scanner.Hosts.Add(Host("192.168.1.40", Camera));
        await factory.ScanAsync();

        Assert.False((await factory.DevicesAsync()).Single(device => device.Mac == Camera).Known);
        Assert.Single(await factory.EventsAsync());
        Assert.Empty(await factory.AlertsAsync());
        Assert.Empty(factory.Provider.Sent);
    }

    [Fact]
    public async Task A_changed_address_is_recorded_and_names_are_kept_when_a_scan_cannot_find_them()
    {
        await using var factory = new LanApiFactory();
        _ = factory.CreateClient();
        await SeedBaselineAsync(factory);

        factory.Scanner.Hosts.Clear();
        factory.Scanner.Hosts.AddRange([Host("192.168.1.1", Router), Host("192.168.1.99", Laptop), Host("192.168.1.30", Phone)]);
        await factory.ScanAsync();

        var laptop = (await factory.DevicesAsync()).Single(device => device.Mac == Laptop);
        Assert.Equal("192.168.1.99", laptop.CurrentIp);
        Assert.Equal("ali-laptop", laptop.Hostname);
        var change = Assert.Single(await factory.EventsAsync());
        Assert.Equal(DeviceEventType.IpChanged, change.Type);
        Assert.Equal("192.168.1.20 → 192.168.1.99", change.Detail);
    }

    [Fact]
    public async Task A_device_goes_offline_only_after_enough_missed_scans_and_comes_back()
    {
        await using var factory = new LanApiFactory();
        _ = factory.CreateClient();
        await SeedBaselineAsync(factory);
        factory.Scanner.Hosts.RemoveAll(host => host.Mac == Laptop);

        await factory.ScansAsync(2);
        Assert.True((await factory.DevicesAsync()).Single(device => device.Mac == Laptop).Online);

        await factory.ScanAsync();
        var offline = (await factory.DevicesAsync()).Single(device => device.Mac == Laptop);
        Assert.False(offline.Online);
        Assert.Equal(DeviceEventType.Offline, Assert.Single(await factory.EventsAsync()).Type);
        Assert.Empty(await factory.AlertsAsync());

        factory.Scanner.Hosts.Add(Host("192.168.1.20", Laptop));
        await factory.ScanAsync();

        Assert.True((await factory.DevicesAsync()).Single(device => device.Mac == Laptop).Online);
        Assert.Equal([DeviceEventType.Offline, DeviceEventType.Online], (await factory.EventsAsync()).Select(e => e.Type));
    }

    [Fact]
    public async Task A_device_that_flickers_never_goes_offline()
    {
        await using var factory = new LanApiFactory();
        _ = factory.CreateClient();
        await SeedBaselineAsync(factory);
        var laptop = factory.Scanner.Hosts.Single(host => host.Mac == Laptop);

        for (var round = 0; round < 4; round++)
        {
            factory.Scanner.Hosts.Remove(laptop);
            await factory.ScansAsync(2);
            factory.Scanner.Hosts.Add(laptop);
            await factory.ScanAsync();
        }

        Assert.True((await factory.DevicesAsync()).Single(device => device.Mac == Laptop).Online);
        Assert.Empty(await factory.EventsAsync());
    }

    [Fact]
    public async Task A_watched_device_alerts_when_it_goes_offline_and_again_when_it_returns()
    {
        await using var factory = new LanApiFactory();
        _ = factory.CreateClient();
        await factory.AddWebhookDestinationAsync();
        await SeedBaselineAsync(factory);
        var laptopId = (await factory.DevicesAsync()).Single(device => device.Mac == Laptop).Id;
        await factory.SetAsync(laptopId, device => device.Watch = true);
        factory.Scanner.Hosts.RemoveAll(host => host.Mac == Laptop);

        await factory.ScansAsync(3);

        var alert = Assert.Single(await factory.AlertsAsync());
        Assert.Equal(DeviceAlertKind.Offline, alert.Kind);
        Assert.Equal(DeviceAlertStatus.Active, alert.Status);
        Assert.Equal("device.offline", Assert.Single(factory.Provider.Sent).Event);

        factory.Scanner.Hosts.Add(Host("192.168.1.20", Laptop));
        await factory.ScanAsync();

        alert = Assert.Single(await factory.AlertsAsync());
        Assert.Equal(DeviceAlertStatus.Resolved, alert.Status);
        Assert.NotNull(alert.EndedAt);
        Assert.Equal(["device.offline", "device.online"], factory.Provider.Sent.Select(message => message.Event));
        Assert.Contains("ali-laptop is back online after", factory.Provider.Sent[1].Body);
        Assert.True(alert.RecoveryNotificationSent);
    }

    [Fact]
    public async Task A_failed_or_empty_scan_leaves_every_device_as_it_was()
    {
        await using var factory = new LanApiFactory();
        _ = factory.CreateClient();
        await SeedBaselineAsync(factory);

        factory.Scanner.Hosts.Clear();
        var empty = await factory.ScanAsync();
        factory.Scanner.Hosts.Add(Host("192.168.1.1", Router));
        factory.Scanner.Error = "ARP table unavailable";
        var errored = await factory.ScanAsync();
        factory.Scanner.Error = null;
        factory.Scanner.Throw = true;
        var crashed = await factory.ScanAsync();
        factory.Scanner.Throw = false;

        Assert.All(new[] { empty, errored, crashed }, outcome => Assert.False(outcome.Run.Success));
        Assert.Equal("No device answered the scan.", empty.Run.Error);
        Assert.Equal("ARP table unavailable", errored.Run.Error);
        Assert.Equal("The scan failed. See the Wanetra log.", crashed.Run.Error);
        Assert.All(await factory.DevicesAsync(), device =>
        {
            Assert.True(device.Online);
            Assert.Equal(0, device.MissedScans);
        });
        Assert.Empty(await factory.EventsAsync());
    }

    [Fact]
    public async Task Duplicate_answers_for_one_device_are_merged()
    {
        await using var factory = new LanApiFactory();
        _ = factory.CreateClient();
        factory.Scanner.Hosts.AddRange([Host("192.168.1.20", Laptop), Host("192.168.1.21", Laptop)]);

        await factory.ScanAsync();

        Assert.Single(await factory.DevicesAsync());
    }

    [Fact]
    public async Task Without_a_usable_network_the_run_says_what_to_do()
    {
        await using var factory = new LanApiFactory();
        _ = factory.CreateClient();
        factory.Detector.Detection = new Wanetra.Application.Lan.NetworkDetection([], true);

        var outcome = await factory.ScanAsync();

        Assert.False(outcome.Run.Success);
        Assert.Contains("compose.lan.yaml", outcome.Run.Error);
        Assert.Empty(factory.Scanner.Scanned);
    }

    [Fact]
    public async Task A_configured_network_replaces_detection_and_is_validated()
    {
        await using var factory = new LanApiFactory();
        var client = factory.CreateClient();
        factory.Scanner.Hosts.Add(Host("10.0.5.4", Laptop));

        var invalid = await client.PutAsJsonAsync("/api/settings", new { values = new Dictionary<string, object?> { ["lan.cidr"] = "8.8.8.0/24" } });
        var tooBig = await client.PutAsJsonAsync("/api/settings", new { values = new Dictionary<string, object?> { ["lan.cidr"] = "10.0.0.0/8" } });
        var garbage = await client.PutAsJsonAsync("/api/settings", new { values = new Dictionary<string, object?> { ["lan.cidr"] = "home" } });
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, tooBig.StatusCode);
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, garbage.StatusCode);

        var valid = await client.PutAsJsonAsync("/api/settings", new { values = new Dictionary<string, object?> { ["lan.cidr"] = "10.0.5.0/24, 192.168.7.0/24" } });
        Assert.True(valid.IsSuccessStatusCode);
        var outcome = await factory.ScanAsync();

        Assert.True(outcome.Run.Success);
        Assert.Equal("10.0.5.0/24, 192.168.7.0/24", outcome.Run.Networks);
        Assert.Equal(["10.0.5.0/24", "192.168.7.0/24"], factory.Scanner.Scanned.Single().Select(range => range.ToString()));
    }

    [Fact]
    public async Task Watching_a_device_adds_a_ping_target_that_follows_its_address()
    {
        await using var factory = new LanApiFactory();
        var client = factory.CreateClient();
        await SeedBaselineAsync(factory);
        var laptopId = (await factory.DevicesAsync()).Single(device => device.Mac == Laptop).Id;

        var watch = await client.PutAsJsonAsync($"/api/devices/{laptopId}", new { watch = true });
        Assert.True(watch.IsSuccessStatusCode);

        var target = (await factory.ProbeTargetsAsync()).Single(t => t.DeviceId == laptopId);
        Assert.Equal("Device: ali-laptop", target.Name);
        Assert.Equal(ProbeKind.Icmp, target.Kind);
        Assert.Equal(ProbeRole.Custom, target.Role);
        Assert.Equal("192.168.1.20", target.Target);

        factory.Scanner.Hosts.Clear();
        factory.Scanner.Hosts.AddRange([Host("192.168.1.1", Router), Host("192.168.1.77", Laptop), Host("192.168.1.30", Phone)]);
        await factory.ScanAsync();
        Assert.Equal("192.168.1.77", (await factory.ProbeTargetsAsync()).Single(t => t.DeviceId == laptopId).Target);

        await client.PutAsJsonAsync($"/api/devices/{laptopId}", new { watch = false });
        Assert.DoesNotContain(await factory.ProbeTargetsAsync(), t => t.DeviceId == laptopId);
    }
}
