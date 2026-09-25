using Wanetra.Application.Alerts;
using Wanetra.Application.Baselines;
using Wanetra.Domain;

namespace Wanetra.Api.Tests.SpeedTests;

public class AlertConditionEvaluatorTests
{
    [Fact]
    public void Any_static_violation_makes_successful_measurement_unhealthy()
    {
        var result = AlertConditionEvaluator.Evaluate(
            new AlertRule { Name = "rule", MinDownloadMbps = 100, MaxLatencyMs = 20 },
            new SpeedTestResult { Engine = "test", Success = true, DownloadMbps = 150, LatencyMs = 21 },
            Baseline());

        Assert.True(result.Unhealthy);
        Assert.Contains("latency", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Unavailable_baseline_is_ignored_while_static_threshold_still_applies()
    {
        var result = AlertConditionEvaluator.Evaluate(
            new AlertRule { Name = "rule", MinDownloadMbps = 100, DownloadBaselineDropPercent = 30 },
            new SpeedTestResult { Engine = "test", Success = true, DownloadMbps = 90 },
            Baseline(downloadAvailable: false));

        Assert.True(result.Unhealthy);
    }

    [Fact]
    public void Failed_measurement_is_a_distinct_unhealthy_execution_failure()
    {
        var result = AlertConditionEvaluator.Evaluate(
            new AlertRule { Name = "rule", MinDownloadMbps = 100 },
            new SpeedTestResult { Engine = "test", Success = false, DownloadMbps = 1 },
            Baseline());

        Assert.Equal(AlertMeasurementOutcome.ExecutionFailure, result.Outcome);
        Assert.True(result.Unhealthy);
        Assert.Equal("speed test failed", result.Reason);
    }

    [Fact]
    public void Unavailable_packet_loss_does_not_trigger_its_configured_threshold()
    {
        var result = AlertConditionEvaluator.Evaluate(
            new AlertRule { Name = "rule", MaxPacketLossPercent = 5 },
            new SpeedTestResult { Engine = "test", Success = true, PacketLossPercent = null },
            Baseline());

        Assert.Equal(AlertMeasurementOutcome.Healthy, result.Outcome);
    }

    [Fact]
    public void Successful_measurement_without_violations_is_healthy()
    {
        var result = AlertConditionEvaluator.Evaluate(
            new AlertRule { Name = "rule" },
            new SpeedTestResult { Engine = "test", Success = true, DownloadMbps = 150 },
            Baseline());

        Assert.Equal(AlertMeasurementOutcome.Healthy, result.Outcome);
        Assert.False(result.Unhealthy);
    }

    private static BaselineSnapshot Baseline(bool downloadAvailable = true) => new(
        DateTime.UtcNow.AddDays(-7), DateTime.UtcNow,
        new MetricBaseline(downloadAvailable, 10, downloadAvailable ? 100 : null, 100, null),
        new MetricBaseline(false, 0, null, null, null));
}
