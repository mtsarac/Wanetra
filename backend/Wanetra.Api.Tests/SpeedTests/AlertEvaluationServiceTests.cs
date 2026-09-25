using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wanetra.Application.Alerts;
using Wanetra.Domain;
using Wanetra.Infrastructure.Persistence;

namespace Wanetra.Api.Tests.SpeedTests;

public class AlertEvaluationServiceTests
{
    [Fact]
    public async Task Pending_progress_survives_scopes_and_opens_only_one_event()
    {
        await using var factory = new WanetraApiFactory();
        await SeedAsync(factory, failures: 2, recoveries: 2);

        await EvaluateInNewScope(factory, Result(50));
        await EvaluateInNewScope(factory, Result(50));
        await EvaluateInNewScope(factory, Result(50));
        await EvaluateInNewScope(factory, Result(40));

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WanetraDbContext>();
        Assert.Single(await db.DegradationEvents.ToListAsync());
        Assert.Equal(DegradationStatus.Active, (await db.DegradationEvents.SingleAsync()).Status);
    }

    [Fact]
    public async Task Interrupted_recovery_resets_progress_then_two_healthy_results_close_event()
    {
        await using var factory = new WanetraApiFactory();
        await SeedAsync(factory, failures: 1, recoveries: 2);

        await EvaluateInNewScope(factory, Result(50));
        await EvaluateInNewScope(factory, Result(150));
        await EvaluateInNewScope(factory, Result(50));
        await EvaluateInNewScope(factory, Result(150));
        await EvaluateInNewScope(factory, Result(150));

        using var scope = factory.Services.CreateScope();
        var degradationEvent = await scope.ServiceProvider.GetRequiredService<WanetraDbContext>().DegradationEvents.SingleAsync();
        Assert.Equal(DegradationStatus.Recovered, degradationEvent.Status);
        Assert.NotNull(degradationEvent.EndedAt);
    }

    [Fact]
    public async Task Failure_then_healthy_measurement_resets_unhealthy_counter()
    {
        await using var factory = new WanetraApiFactory();
        await SeedAsync(factory, failures: 2, recoveries: 2);

        await EvaluateInNewScope(factory, FailedResult("connection refused"));
        await EvaluateInNewScope(factory, Result(150));
        await EvaluateInNewScope(factory, FailedResult("DNS lookup failed"));

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WanetraDbContext>();
        Assert.Empty(await db.DegradationEvents.ToListAsync());
        Assert.Equal(1, (await db.AlertStates.SingleAsync()).ConsecutiveUnhealthyMeasurements);
    }

    [Fact]
    public async Task Consecutive_failures_open_incident_and_healthy_measurements_recover_it()
    {
        await using var factory = new WanetraApiFactory();
        await SeedAsync(factory, failures: 2, recoveries: 2);

        await EvaluateInNewScope(factory, FailedResult("connection refused"));
        await EvaluateInNewScope(factory, FailedResult("DNS lookup failed"));
        await EvaluateInNewScope(factory, Result(150));

        using (var scope = factory.Services.CreateScope())
        {
            var degradationEvent = await scope.ServiceProvider.GetRequiredService<WanetraDbContext>().DegradationEvents.SingleAsync();
            Assert.Equal(DegradationStatus.Recovering, degradationEvent.Status);
            Assert.Equal("speed test failed", degradationEvent.Reason);
            Assert.Equal(0, degradationEvent.ConsecutiveUnhealthyMeasurements);
            Assert.Equal(1, degradationEvent.ConsecutiveHealthyMeasurements);
            Assert.Null(degradationEvent.WorstDownloadMbps);
        }

        await EvaluateInNewScope(factory, Result(150));

        using var recoveryScope = factory.Services.CreateScope();
        var recovered = await recoveryScope.ServiceProvider.GetRequiredService<WanetraDbContext>().DegradationEvents.SingleAsync();
        Assert.Equal(DegradationStatus.Recovered, recovered.Status);
        Assert.NotNull(recovered.EndedAt);
    }

    [Fact]
    public async Task Rule_update_resets_pending_state_without_closing_active_event()
    {
        await using var factory = new WanetraApiFactory();
        await SeedAsync(factory, failures: 2, recoveries: 2);
        await EvaluateInNewScope(factory, Result(50));

        using (var scope = factory.Services.CreateScope())
        {
            var rule = await scope.ServiceProvider.GetRequiredService<WanetraDbContext>().AlertRules.SingleAsync();
            rule.UpdatedAt = rule.UpdatedAt.AddMinutes(1);
            await scope.ServiceProvider.GetRequiredService<WanetraDbContext>().SaveChangesAsync();
        }

        await EvaluateInNewScope(factory, Result(50));
        await EvaluateInNewScope(factory, Result(50));

        using var verificationScope = factory.Services.CreateScope();
        var db = verificationScope.ServiceProvider.GetRequiredService<WanetraDbContext>();
        Assert.Single(await db.DegradationEvents.ToListAsync());
    }

    private static async Task SeedAsync(WanetraApiFactory factory, int failures, int recoveries)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WanetraDbContext>();
        var rule = await db.AlertRules.SingleAsync();
        rule.Name = "default";
        rule.MinDownloadMbps = 100;
        rule.ConsecutiveFailuresRequired = failures;
        rule.ConsecutiveRecoveriesRequired = recoveries;
        await db.SaveChangesAsync();
    }

    private static async Task EvaluateInNewScope(WanetraApiFactory factory, SpeedTestResult result)
    {
        using var scope = factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AlertEvaluationService>().EvaluateAsync(result, CancellationToken.None);
    }

    private static SpeedTestResult Result(double download) => new()
    {
        Engine = "test",
        Success = true,
        Timestamp = DateTime.UtcNow,
        DownloadMbps = download,
    };
    private static SpeedTestResult FailedResult(string errorMessage) => new()
    {
        Engine = "test",
        Success = false,
        ErrorMessage = errorMessage,
        Timestamp = DateTime.UtcNow,
    };
}
