using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Wanetra.Api.Contracts;
using Wanetra.Application.Probes;
using Wanetra.Domain;

namespace Wanetra.Api.Tests.Probes;

public class ProbeApiTests
{
    private static async Task<List<ProbeTargetResponse>> TargetsAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<List<ProbeTargetResponse>>("/api/probes/targets"))!;

    private static ProbeTargetRequest Icmp(string name, string host) => new(name, "icmp", "custom", host, null, null, true);

    [Fact]
    public async Task A_fresh_install_has_the_default_targets_and_restarts_do_not_recreate_deleted_ones()
    {
        await using var factory = new ProbeApiFactory();
        var client = factory.CreateClient();

        var targets = await TargetsAsync(client);
        Assert.Equal(["Gateway", "Cloudflare 1.1.1.1", "Google 8.8.8.8", "Quad9 9.9.9.9", "DNS lookup"], targets.Select(target => target.Name));
        Assert.Equal("gateway", targets[0].Role);
        Assert.Null(targets[0].Target);
        Assert.Equal("dns", targets[4].Kind);

        foreach (var target in targets)
        {
            Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/probes/targets/{target.Id}")).StatusCode);
        }

        await factory.WithScopeAsync(async services =>
        {
            await services.GetRequiredService<ProbeTargetService>().EnsureDefaultsAsync(CancellationToken.None);
            return 0;
        });
        Assert.Empty(await TargetsAsync(client));
    }

    [Fact]
    public async Task Targets_can_be_created_updated_and_deleted()
    {
        await using var factory = new ProbeApiFactory();
        var client = factory.CreateClient();

        var created = await client.PostAsJsonAsync("/api/probes/targets", new ProbeTargetRequest("NAS", "tcp", "custom", "192.168.1.20", 445, null, true));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var target = (await created.Content.ReadFromJsonAsync<ProbeTargetResponse>())!;
        Assert.Equal("tcp", target.Kind);
        Assert.Equal(445, target.Port);

        var updated = await client.PutAsJsonAsync($"/api/probes/targets/{target.Id}", new ProbeTargetRequest("NAS (SMB)", "tcp", "custom", "nas.lan", 445, null, false));
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        var after = (await updated.Content.ReadFromJsonAsync<ProbeTargetResponse>())!;
        Assert.Equal("NAS (SMB)", after.Name);
        Assert.False(after.Enabled);

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/probes/targets/{target.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"/api/probes/targets/{target.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync($"/api/probes/targets/{target.Id}", Icmp("x", "1.1.1.1"))).StatusCode);
    }

    public static TheoryData<ProbeTargetRequest> InvalidTargets => new()
    {
        new ProbeTargetRequest("", "icmp", "custom", "1.1.1.1", null, null, true),
        new ProbeTargetRequest(new string('a', 65), "icmp", "custom", "1.1.1.1", null, null, true),
        new ProbeTargetRequest("x", "ping", "custom", "1.1.1.1", null, null, true),
        new ProbeTargetRequest("x", "icmp", "boss", "1.1.1.1", null, null, true),
        new ProbeTargetRequest("x", "icmp", "custom", null, null, null, true),
        new ProbeTargetRequest("x", "icmp", "custom", "not a host", null, null, true),
        new ProbeTargetRequest("x", "icmp", "custom", "-bad-.example", null, null, true),
        new ProbeTargetRequest("x", "tcp", "custom", "1.1.1.1", null, null, true),
        new ProbeTargetRequest("x", "tcp", "custom", "1.1.1.1", 70000, null, true),
        new ProbeTargetRequest("x", "dns", "internet", "example.com", null, null, true),
        new ProbeTargetRequest("x", "icmp", "dns", "1.1.1.1", null, null, true),
        new ProbeTargetRequest("x", "dns", "dns", "1.2.3.4", null, null, true),
        new ProbeTargetRequest("x", "dns", "dns", "example.com", null, "resolver", true),
        new ProbeTargetRequest("x", "http", "custom", "ftp://example.com", null, null, true),
        new ProbeTargetRequest("x", "http", "custom", "https://user:pass@example.com/", null, null, true),
        new ProbeTargetRequest("x", "http", "gateway", "http://example.com", null, null, true),
    };

    [Theory]
    [MemberData(nameof(InvalidTargets))]
    public async Task Invalid_targets_are_rejected(ProbeTargetRequest request)
    {
        await using var factory = new ProbeApiFactory();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/probes/targets", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("invalid_request", (await response.Content.ReadFromJsonAsync<ApiError>())!.Code);
        Assert.Equal(5, (await TargetsAsync(client)).Count);
    }

    [Fact]
    public async Task Target_names_are_unique_ignoring_case_and_the_total_is_capped()
    {
        await using var factory = new ProbeApiFactory();
        var client = factory.CreateClient();

        var duplicate = await client.PostAsJsonAsync("/api/probes/targets", Icmp("gateway", "192.168.1.1"));
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal("probe_target_duplicate", (await duplicate.Content.ReadFromJsonAsync<ApiError>())!.Code);

        for (var i = 0; i < ProbeTargetService.MaxTargets - 5; i++)
        {
            Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/probes/targets", Icmp($"extra {i}", "10.0.0.1"))).StatusCode);
        }

        var over = await client.PostAsJsonAsync("/api/probes/targets", Icmp("one too many", "10.0.0.1"));
        Assert.Equal(HttpStatusCode.Conflict, over.StatusCode);
        Assert.Equal("probe_target_limit", (await over.Content.ReadFromJsonAsync<ApiError>())!.Code);
    }

    [Fact]
    public async Task A_missing_body_is_a_bad_request()
    {
        await using var factory = new ProbeApiFactory();
        var client = factory.CreateClient();

        var response = await client.PostAsync("/api/probes/targets", new StringContent("null", System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Status_reports_the_chain_per_target_and_an_open_outage()
    {
        await using var factory = new ProbeApiFactory();
        var client = factory.CreateClient();

        var before = (await client.GetFromJsonAsync<ConnectivityStatusResponse>("/api/probes/status"))!;
        Assert.Equal("unknown", before.State);
        Assert.True(before.Monitoring);

        await factory.CyclesAsync(2);
        var healthy = (await client.GetFromJsonAsync<ConnectivityStatusResponse>("/api/probes/status"))!;
        Assert.Equal("healthy", healthy.State);
        Assert.Equal("up", healthy.Layers.Gateway);
        Assert.Equal("up", healthy.Layers.Internet);
        Assert.Equal("up", healthy.Layers.Dns);
        Assert.Equal(5, healthy.Targets.Count);
        Assert.All(healthy.Targets, target =>
        {
            Assert.True(target.Up);
            Assert.Equal(12, target.RttMs);
            Assert.Equal(0, target.LossPercent);
        });
        Assert.Null(healthy.ActiveOutage);

        factory.Executor.Failing(request => request.Kind != ProbeKind.Dns && request.Host != ProbeApiFactory.Gateway);
        await factory.CycleAsync();
        var degraded = (await client.GetFromJsonAsync<ConnectivityStatusResponse>("/api/probes/status"))!;
        Assert.Equal("degraded", degraded.State);
        Assert.Equal("wan", degraded.Scope);

        await factory.CyclesAsync(2);
        var outage = (await client.GetFromJsonAsync<ConnectivityStatusResponse>("/api/probes/status"))!;
        Assert.Equal("outage", outage.State);
        Assert.NotNull(outage.ActiveOutage);
        Assert.Equal("wan", outage.ActiveOutage.Scope);
        Assert.Equal("down", outage.Layers.Internet);
        Assert.True(outage.Targets.Single(target => target.Target.Name == "Cloudflare 1.1.1.1").LossPercent > 0);
    }

    [Fact]
    public async Task Status_treats_stale_samples_as_unknown()
    {
        await using var factory = new ProbeApiFactory();
        var client = factory.CreateClient();
        await factory.CycleAsync();

        factory.Time.Advance(TimeSpan.FromMinutes(2));

        Assert.Equal("unknown", (await client.GetFromJsonAsync<ConnectivityStatusResponse>("/api/probes/status"))!.State);
    }

    [Fact]
    public async Task Series_defaults_to_raw_samples_for_a_short_range()
    {
        await using var factory = new ProbeApiFactory();
        var client = factory.CreateClient();
        await factory.CyclesAsync(3);
        var now = factory.Time.GetUtcNow().UtcDateTime;
        var from = Uri.EscapeDataString(now.AddMinutes(-10).ToString("O"));

        var series = (await client.GetFromJsonAsync<ProbeSeriesResponse>($"/api/probes/series?targetId=2&from={from}"))!;

        Assert.Equal("raw", series.Resolution);
        Assert.Equal(3, series.Points.Count);
        Assert.All(series.Points, point => Assert.Equal(12, point.RttMs));
    }

    [Fact]
    public async Task Series_accepts_offset_timestamps()
    {
        await using var factory = new ProbeApiFactory();
        var client = factory.CreateClient();
        await factory.CyclesAsync(2);
        var from = Uri.EscapeDataString("2026-10-18T14:59:00+03:00");
        var to = Uri.EscapeDataString("2026-10-18T15:10:00+03:00");

        var series = (await client.GetFromJsonAsync<ProbeSeriesResponse>($"/api/probes/series?targetId=2&from={from}&to={to}"))!;

        Assert.Equal(2, series.Points.Count);
    }

    [Fact]
    public async Task Series_uses_rollups_for_longer_ranges()
    {
        await using var factory = new ProbeApiFactory();
        var client = factory.CreateClient();
        await factory.CyclesAsync(12);
        factory.Time.Advance(TimeSpan.FromMinutes(3));
        await factory.WithScopeAsync(async services =>
        {
            await services.GetRequiredService<ProbeRollupService>().RollUpAsync(CancellationToken.None);
            return 0;
        });
        var now = factory.Time.GetUtcNow().UtcDateTime;
        var from = Uri.EscapeDataString(now.AddHours(-12).ToString("O"));

        var series = (await client.GetFromJsonAsync<ProbeSeriesResponse>($"/api/probes/series?targetId=2&from={from}"))!;

        Assert.Equal("minute", series.Resolution);
        Assert.Equal(2, series.Points.Count);
        Assert.Equal(6, series.Points[0].Sent);
        Assert.Equal(0, series.Points[0].LossPercent);

        var hourly = (await client.GetFromJsonAsync<ProbeSeriesResponse>(
            $"/api/probes/series?targetId=2&from={Uri.EscapeDataString(now.AddDays(-5).ToString("O"))}"))!;
        Assert.Equal("hour", hourly.Resolution);
    }

    [Theory]
    [InlineData("/api/probes/series")]
    [InlineData("/api/probes/series?targetId=2&resolution=weekly")]
    [InlineData("/api/probes/series?targetId=2&from=2026-10-18T12:00:00Z&to=2026-10-18T11:00:00Z")]
    [InlineData("/api/probes/series?targetId=2&from=2026-10-01T00:00:00Z&to=2026-10-18T00:00:00Z&resolution=raw")]
    public async Task Series_rejects_bad_queries(string url)
    {
        await using var factory = new ProbeApiFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Outages_are_listed_newest_first_with_their_duration()
    {
        await using var factory = new ProbeApiFactory();
        var client = factory.CreateClient();
        var now = factory.Time.GetUtcNow().UtcDateTime;
        await factory.WithDbAsync(async db =>
        {
            db.Outages.AddRange(
                new Outage { StartedAt = now.AddHours(-5), EndedAt = now.AddHours(-5).AddMinutes(10), Status = OutageStatus.Recovered, Scope = OutageScope.Wan, Reason = "old", LastObservedAt = now },
                new Outage { StartedAt = now.AddHours(-1), EndedAt = null, Status = OutageStatus.Active, Scope = OutageScope.Dns, Reason = "new", LastObservedAt = now });
            await db.SaveChangesAsync();
        });

        var outages = (await client.GetFromJsonAsync<List<OutageResponse>>("/api/outages?count=5"))!;

        Assert.Equal(["new", "old"], outages.Select(outage => outage.Reason));
        Assert.Equal("dns", outages[0].Scope);
        Assert.Equal(3600, outages[0].DurationSeconds);
        Assert.Equal(600, outages[1].DurationSeconds);
        Assert.Equal("recovered", outages[1].Status);
    }

    [Fact]
    public async Task Uptime_is_derived_from_the_recorded_outages_and_clipped_to_the_window()
    {
        await using var factory = new ProbeApiFactory();
        var client = factory.CreateClient();
        var now = factory.Time.GetUtcNow().UtcDateTime;
        await factory.WithDbAsync(async db =>
        {
            db.AppSettings.Add(new AppSetting
            {
                Key = ProbeInternalKeys.MonitoringSince,
                Value = now.AddDays(-60).ToString("O"),
                UpdatedAt = now,
            });
            db.Outages.AddRange(
                new Outage { StartedAt = now.AddHours(-30), EndedAt = now.AddHours(-23), Status = OutageStatus.Recovered, Scope = OutageScope.Wan, Reason = "straddles the window start", LastObservedAt = now },
                new Outage { StartedAt = now.AddHours(-2), EndedAt = now.AddHours(-2).AddMinutes(30), Status = OutageStatus.Recovered, Scope = OutageScope.Wan, Reason = "inside", LastObservedAt = now },
                new Outage { StartedAt = now.AddMinutes(-30), EndedAt = null, Status = OutageStatus.Active, Scope = OutageScope.Wan, Reason = "open", LastObservedAt = now },
                new Outage { StartedAt = now.AddDays(-10), EndedAt = now.AddDays(-10).AddHours(1), Status = OutageStatus.Recovered, Scope = OutageScope.Dns, Reason = "last week", LastObservedAt = now });
            await db.SaveChangesAsync();
        });

        var day = (await client.GetFromJsonAsync<UptimeResponse>("/api/uptime?range=24h"))!;

        Assert.Equal(3, day.OutageCount);
        Assert.Equal(3600 + 1800 + 1800, day.TotalOutageSeconds);
        Assert.Equal(3600, day.LongestOutageSeconds);
        Assert.Equal(100 * (1 - 7200.0 / 86400), day.UptimePercent, precision: 6);
        Assert.True(day.CoversFullRange);

        var month = (await client.GetFromJsonAsync<UptimeResponse>("/api/uptime?range=30d"))!;
        Assert.Equal(4, month.OutageCount);
    }

    [Fact]
    public async Task Uptime_only_covers_the_time_monitoring_has_been_running()
    {
        await using var factory = new ProbeApiFactory();
        var client = factory.CreateClient();
        await factory.CycleAsync();
        factory.Time.Advance(TimeSpan.FromHours(1));

        var week = (await client.GetFromJsonAsync<UptimeResponse>("/api/uptime?range=7d"))!;

        Assert.False(week.CoversFullRange);
        Assert.Equal(100, week.UptimePercent);
        Assert.InRange((week.To - week.From).TotalMinutes, 60, 62);
    }

    [Fact]
    public async Task Uptime_rejects_an_unknown_range()
    {
        await using var factory = new ProbeApiFactory();
        var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/uptime?range=1y")).StatusCode);
        var json = JsonDocument.Parse(await (await client.GetAsync("/api/uptime")).Content.ReadAsStringAsync());
        Assert.Equal("24h", json.RootElement.GetProperty("range").GetString());
    }

    [Fact]
    public async Task Probe_settings_are_exposed_with_their_defaults()
    {
        await using var factory = new ProbeApiFactory();
        var client = factory.CreateClient();

        var settings = (await client.GetFromJsonAsync<SettingsListResponse>("/api/settings"))!.Settings
            .Where(setting => setting.Group == "probes")
            .ToDictionary(setting => setting.Key);

        Assert.Equal(7, settings.Count);
        Assert.Equal(10, ((JsonElement)settings["probes.intervalSeconds"].Value!).GetInt32());
        Assert.True(settings["probes.enabled"].Locked);
    }
}
