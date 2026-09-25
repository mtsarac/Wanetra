using System.Net;
using System.Net.Http.Json;
using Wanetra.Api.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wanetra.Domain;
using Wanetra.Infrastructure.Persistence;

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
    public async Task Disabling_alert_rule_closes_active_incident_and_reenable_does_not_reopen_it()
    {
        await using var factory = new WanetraApiFactory();
        using var client = factory.CreateClient();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanetraDbContext>();
            db.DegradationEvents.Add(new DegradationEvent
            {
                StartedAt = DateTime.UtcNow.AddMinutes(-5),
                Status = DegradationStatus.Recovering,
                Reason = "network test failed",
                ConsecutiveHealthyMeasurements = 1,
            });
            var state = await db.AlertStates.SingleAsync();
            state.ConsecutiveHealthyMeasurements = 1;
            state.ConsecutiveUnhealthyMeasurements = 2;
            await db.SaveChangesAsync();
        }

        var rule = await client.GetFromJsonAsync<AlertRuleResponse>("/api/alerts/rule");
        Assert.NotNull(rule);
        var update = new AlertRuleUpdateRequest(
            rule.Name,
            false,
            rule.MinDownloadMbps,
            rule.MinUploadMbps,
            rule.MaxLatencyMs,
            rule.MaxJitterMs,
            rule.MaxPacketLossPercent,
            rule.DownloadBaselineDropPercent,
            rule.UploadBaselineDropPercent,
            rule.ConsecutiveFailuresRequired,
            rule.ConsecutiveRecoveriesRequired);
        var disabled = await client.PutAsJsonAsync("/api/alerts/rule", update);
        Assert.Equal(HttpStatusCode.OK, disabled.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/alerts/active")).StatusCode);

        var events = await client.GetFromJsonAsync<DegradationEventResponse[]>("/api/alerts/events");
        var incident = Assert.Single(events!);
        Assert.Equal("disabled", incident.Status);
        Assert.Equal("Alert rule disabled", incident.ClosureReason);
        Assert.NotNull(incident.EndedAt);
        var metrics = await client.GetStringAsync("/metrics");
        Assert.Contains("wanetra_connection_degraded 0", metrics);

        var enabled = await client.PutAsJsonAsync("/api/alerts/rule", update with { Enabled = true });
        Assert.Equal(HttpStatusCode.OK, enabled.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/alerts/active")).StatusCode);
        using var verification = factory.Services.CreateScope();
        var finalState = await verification.ServiceProvider.GetRequiredService<WanetraDbContext>().AlertStates.SingleAsync();
        Assert.Equal(0, finalState.ConsecutiveHealthyMeasurements);
        Assert.Equal(0, finalState.ConsecutiveUnhealthyMeasurements);
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
