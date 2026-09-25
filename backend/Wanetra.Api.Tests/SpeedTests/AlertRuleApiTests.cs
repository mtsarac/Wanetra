using System.Net;
using System.Net.Http.Json;
using Wanetra.Api.Contracts;

namespace Wanetra.Api.Tests.SpeedTests;

public class AlertRuleApiTests
{
    [Fact]
    public async Task Default_alert_rule_can_be_updated_and_read_back()
    {
        await using var factory = new WanetraApiFactory();
        using var client = factory.CreateClient();

        var initial = await client.GetFromJsonAsync<AlertRuleResponse>("/api/alerts/rule");
        Assert.NotNull(initial);
        Assert.Equal("WAN health", initial.Name);
        Assert.True(initial.Enabled);
        Assert.Equal(30, initial.DownloadBaselineDropPercent);
        Assert.Equal(3, initial.ConsecutiveFailuresRequired);
        Assert.Equal(2, initial.ConsecutiveRecoveriesRequired);

        var request = new AlertRuleUpdateRequest(
            "WAN health",
            true,
            100,
            null,
            50,
            10,
            5,
            30,
            null,
            3,
            2);
        var savedResponse = await client.PutAsJsonAsync("/api/alerts/rule", request);
        var saved = await savedResponse.Content.ReadFromJsonAsync<AlertRuleResponse>();

        Assert.Equal(HttpStatusCode.OK, savedResponse.StatusCode);
        Assert.NotNull(saved);
        Assert.Equal(initial.Id, saved.Id);
        Assert.Equal(100, saved.MinDownloadMbps);

        var update = request with { Enabled = false, MinDownloadMbps = 120 };
        var updatedResponse = await client.PutAsJsonAsync("/api/alerts/rule", update);
        var updated = await updatedResponse.Content.ReadFromJsonAsync<AlertRuleResponse>();

        Assert.Equal(HttpStatusCode.OK, updatedResponse.StatusCode);
        Assert.NotNull(updated);
        Assert.Equal(saved.Id, updated.Id);
        Assert.False(updated.Enabled);
        Assert.Equal(120, updated.MinDownloadMbps);

        var readBack = await client.GetFromJsonAsync<AlertRuleResponse>("/api/alerts/rule");
        Assert.Equal(updated, readBack);
    }

    [Fact]
    public async Task Alert_rule_rejects_invalid_threshold_configuration()
    {
        await using var factory = new WanetraApiFactory();
        using var client = factory.CreateClient();
        var request = new AlertRuleUpdateRequest(
            "WAN health",
            true,
            -1,
            null,
            null,
            null,
            101,
            null,
            null,
            0,
            1);

        var response = await client.PutAsJsonAsync("/api/alerts/rule", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
