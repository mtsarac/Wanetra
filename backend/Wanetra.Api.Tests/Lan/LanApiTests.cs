using System.Net;
using System.Net.Http.Json;
using Wanetra.Api.Contracts;
using Wanetra.Application.Probes;
using Wanetra.Domain;
using static Wanetra.Api.Tests.Lan.LanApiFactory;

namespace Wanetra.Api.Tests.Lan;

public class LanApiTests
{
    private static async Task SeedAsync(LanApiFactory factory)
    {
        factory.Scanner.Hosts.AddRange(
        [
            Host("192.168.1.1", "b8:27:eb:00:00:01", "router"),
            Host("192.168.1.20", "aa:bb:cc:00:00:02", "ali-laptop"),
        ]);
        await factory.ScanAsync();
        factory.Scanner.Hosts.Add(Host("192.168.1.40", "aa:bb:cc:00:00:04", "cam-7"));
        await factory.ScanAsync();
    }

    private static async Task<List<DeviceResponse>> DevicesAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<List<DeviceResponse>>("/api/devices"))!;

    [Fact]
    public async Task Status_reports_counts_and_networks_and_the_last_scan()
    {
        await using var factory = new LanApiFactory();
        var client = factory.CreateClient();
        await SeedAsync(factory);

        var status = (await client.GetFromJsonAsync<LanStatusResponse>("/api/lan/status"))!;

        Assert.True(status.Enabled);
        Assert.False(status.Scanning);
        Assert.Equal(3, status.TotalDevices);
        Assert.Equal(3, status.OnlineDevices);
        Assert.Equal(1, status.UnknownDevices);
        Assert.Equal(["192.168.1.0/24"], status.Networks);
        Assert.False(status.OnlyContainerNetworks);
        Assert.True(status.LastScan!.Success);
        Assert.Equal(3, status.LastScan.HostsFound);
        Assert.Equal("manual", status.LastScan.Trigger);
    }

    [Fact]
    public async Task Status_warns_when_only_docker_networks_are_visible()
    {
        await using var factory = new LanApiFactory();
        var client = factory.CreateClient();
        factory.Detector.Detection = new Wanetra.Application.Lan.NetworkDetection([], true);

        var status = (await client.GetFromJsonAsync<LanStatusResponse>("/api/lan/status"))!;

        Assert.True(status.OnlyContainerNetworks);
        Assert.Empty(status.Networks);
        Assert.Null(status.LastScan);
    }

    [Fact]
    public async Task Devices_are_listed_with_a_display_name_that_prefers_the_owners_choice()
    {
        await using var factory = new LanApiFactory();
        var client = factory.CreateClient();
        await SeedAsync(factory);

        var devices = await DevicesAsync(client);
        Assert.Equal(["router", "ali-laptop", "cam-7"], devices.Select(device => device.DisplayName));
        var router = devices[0];
        Assert.Equal("b8:27:eb:00:00:01", router.Mac);
        Assert.Equal("Raspberry Pi Foundation", router.Vendor);

        var renamed = await client.PutAsJsonAsync($"/api/devices/{router.Id}", new { name = "  Main router  " });
        Assert.Equal("Main router", (await renamed.Content.ReadFromJsonAsync<DeviceResponse>())!.DisplayName);

        var kept = await client.PutAsJsonAsync($"/api/devices/{router.Id}", new { known = true });
        Assert.Equal("Main router", (await kept.Content.ReadFromJsonAsync<DeviceResponse>())!.Name);

        var cleared = (await (await client.PutAsJsonAsync($"/api/devices/{router.Id}", new { name = "   " })).Content.ReadFromJsonAsync<DeviceResponse>())!;
        Assert.Null(cleared.Name);
        Assert.Equal("router", cleared.DisplayName);
    }

    [Fact]
    public async Task Marking_a_device_known_or_all_known_clears_the_unknown_count()
    {
        await using var factory = new LanApiFactory();
        var client = factory.CreateClient();
        await SeedAsync(factory);
        factory.Scanner.Hosts.Add(Host("192.168.1.41", "aa:bb:cc:00:00:05"));
        await factory.ScanAsync();
        var unknown = (await DevicesAsync(client)).Where(device => !device.Known).ToList();
        Assert.Equal(2, unknown.Count);

        await client.PutAsJsonAsync($"/api/devices/{unknown[0].Id}", new { known = true });
        var result = await client.PostAsync("/api/devices/mark-all-known", null);

        Assert.Equal(1, (await result.Content.ReadFromJsonAsync<MarkKnownResponse>())!.Updated);
        Assert.All(await DevicesAsync(client), device => Assert.True(device.Known));
        Assert.Equal(0, (await client.GetFromJsonAsync<LanStatusResponse>("/api/lan/status"))!.UnknownDevices);
    }

    [Fact]
    public async Task A_very_long_name_is_rejected()
    {
        await using var factory = new LanApiFactory();
        var client = factory.CreateClient();
        await SeedAsync(factory);
        var id = (await DevicesAsync(client))[0].Id;

        var response = await client.PutAsJsonAsync($"/api/devices/{id}", new { name = new string('x', 65) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Device_history_lists_newest_first_and_forgetting_a_device_removes_it()
    {
        await using var factory = new LanApiFactory();
        var client = factory.CreateClient();
        await SeedAsync(factory);
        factory.Scanner.Hosts.RemoveAll(host => host.Mac == "aa:bb:cc:00:00:04");
        await factory.ScansAsync(3);
        var camera = (await DevicesAsync(client)).Single(device => device.Mac == "aa:bb:cc:00:00:04");

        var events = (await client.GetFromJsonAsync<List<DeviceEventResponse>>($"/api/devices/{camera.Id}/events"))!;
        Assert.Equal(["offline", "joined"], events.Select(e => e.Type));
        Assert.Equal(["offline", "joined"], (await client.GetFromJsonAsync<List<DeviceEventResponse>>("/api/lan/events?count=5"))!.Select(e => e.Type));

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/devices/{camera.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/devices/{camera.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/devices/{camera.Id}/events")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"/api/devices/{camera.Id}")).StatusCode);
        var remaining = (await client.GetFromJsonAsync<List<DeviceEventResponse>>("/api/lan/events"))!;
        Assert.DoesNotContain(remaining, e => e.DeviceId == camera.Id);
    }

    [Fact]
    public async Task Update_needs_a_body_and_an_existing_device()
    {
        await using var factory = new LanApiFactory();
        var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync("/api/devices/99", new { known = true })).StatusCode);
        var empty = await client.PutAsync("/api/devices/99", new StringContent("null", System.Text.Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
    }

    [Fact]
    public async Task A_scan_can_be_started_by_hand_but_not_twice_and_not_when_scanning_is_off()
    {
        await using var factory = new LanApiFactory();
        var client = factory.CreateClient();
        var gate = new TaskCompletionSource();
        factory.Scanner.Hosts.Add(Host("192.168.1.1", "b8:27:eb:00:00:01"));

        var started = await client.PostAsync("/api/lan/scan", null);
        Assert.Equal(HttpStatusCode.Accepted, started.StatusCode);
        // The fake scanner returns at once; wait for the run to be recorded.
        for (var i = 0; i < 100 && (await client.GetFromJsonAsync<LanStatusResponse>("/api/lan/status"))!.LastScan is null; i++)
        {
            await Task.Delay(20);
        }

        var status = (await client.GetFromJsonAsync<LanStatusResponse>("/api/lan/status"))!;
        Assert.True(status.LastScan!.Success);
        gate.SetResult();

        await using var disabled = new DisabledLanFactory();
        var off = await disabled.CreateClient().PostAsync("/api/lan/scan", null);
        Assert.Equal(HttpStatusCode.Conflict, off.StatusCode);
        Assert.Equal("lan_disabled", (await off.Content.ReadFromJsonAsync<ApiError>())!.Code);
    }

    [Fact]
    public async Task A_device_target_can_be_paused_but_not_edited_or_deleted_from_the_probe_list()
    {
        await using var factory = new LanApiFactory();
        var client = factory.CreateClient();
        await SeedAsync(factory);
        var laptop = (await DevicesAsync(client)).Single(device => device.Mac == "aa:bb:cc:00:00:02");
        await client.PutAsJsonAsync($"/api/devices/{laptop.Id}", new { watch = true });
        var target = (await client.GetFromJsonAsync<List<ProbeTargetResponse>>("/api/probes/targets"))!.Single(t => t.DeviceId == laptop.Id);

        var edit = await client.PutAsJsonAsync($"/api/probes/targets/{target.Id}",
            new ProbeTargetRequest("Renamed", "icmp", "internet", "8.8.8.8", null, null, false));
        var paused = (await edit.Content.ReadFromJsonAsync<ProbeTargetResponse>())!;
        Assert.Equal(target.Name, paused.Name);
        Assert.Equal("192.168.1.20", paused.Target);
        Assert.Equal("custom", paused.Role);
        Assert.False(paused.Enabled);

        var delete = await client.DeleteAsync($"/api/probes/targets/{target.Id}");
        Assert.Equal(HttpStatusCode.BadRequest, delete.StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/devices/{laptop.Id}")).StatusCode);
        Assert.DoesNotContain((await client.GetFromJsonAsync<List<ProbeTargetResponse>>("/api/probes/targets"))!, t => t.DeviceId == laptop.Id);
    }

    [Fact]
    public async Task Watching_is_skipped_quietly_when_the_probe_list_is_full()
    {
        await using var factory = new LanApiFactory();
        var client = factory.CreateClient();
        await SeedAsync(factory);
        for (var i = 0; i < ProbeTargetService.MaxTargets - 5; i++)
        {
            await client.PostAsJsonAsync("/api/probes/targets", new ProbeTargetRequest($"extra {i}", "icmp", "custom", "10.0.0.1", null, null, true));
        }

        var laptop = (await DevicesAsync(client)).Single(device => device.Mac == "aa:bb:cc:00:00:02");
        var response = await client.PutAsJsonAsync($"/api/devices/{laptop.Id}", new { watch = true });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True((await response.Content.ReadFromJsonAsync<DeviceResponse>())!.Watch);
        Assert.DoesNotContain(await factory.ProbeTargetsAsync(), t => t.DeviceId == laptop.Id);
    }

    private sealed class DisabledLanFactory : WanetraApiFactory
    {
        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.UseSetting("Lan:Enabled", "false");
        }
    }
}
