using Wanetra.Application.Baselines;
using Wanetra.Domain;

namespace Wanetra.Application.Alerts;

public enum AlertMeasurementOutcome
{
    Healthy,
    Unhealthy,
    ExecutionFailure,
}

public sealed record AlertEvaluationResult(AlertMeasurementOutcome Outcome, string Reason)
{
    public bool Unhealthy => Outcome != AlertMeasurementOutcome.Healthy;
}

public static class AlertConditionEvaluator
{
    public static AlertEvaluationResult Evaluate(
        AlertRule rule,
        SpeedTestResult result,
        BaselineSnapshot baseline)
    {
        if (!result.Success)
        {
            return new AlertEvaluationResult(AlertMeasurementOutcome.ExecutionFailure, "speed test failed");
        }

        var reasons = new List<string>();
        AddViolation(result.DownloadMbps < rule.MinDownloadMbps, reasons, "download below threshold");
        AddViolation(result.UploadMbps < rule.MinUploadMbps, reasons, "upload below threshold");
        AddViolation(result.LatencyMs > rule.MaxLatencyMs, reasons, "latency above threshold");
        AddViolation(result.JitterMs > rule.MaxJitterMs, reasons, "jitter above threshold");
        AddViolation(result.PacketLossPercent > rule.MaxPacketLossPercent, reasons, "packet loss above threshold");
        AddBaselineViolation(result.DownloadMbps, baseline.Download, rule.DownloadBaselineDropPercent, reasons, "download below baseline");
        AddBaselineViolation(result.UploadMbps, baseline.Upload, rule.UploadBaselineDropPercent, reasons, "upload below baseline");

        return reasons.Count > 0
            ? new AlertEvaluationResult(AlertMeasurementOutcome.Unhealthy, string.Join(", ", reasons))
            : new AlertEvaluationResult(AlertMeasurementOutcome.Healthy, string.Empty);
    }


    private static void AddViolation(bool violation, List<string> reasons, string reason)
    {
        if (violation)
        {
            reasons.Add(reason);
        }
    }

    private static void AddBaselineViolation(
        double? current,
        MetricBaseline baseline,
        double? dropThreshold,
        List<string> reasons,
        string reason)
    {
        if (!current.HasValue || !baseline.Available || !baseline.BaselineMbps.HasValue || baseline.BaselineMbps.Value <= 0 || !dropThreshold.HasValue)
        {
            return;
        }

        var drop = (baseline.BaselineMbps.Value - current.Value) / baseline.BaselineMbps.Value * 100;
        AddViolation(drop >= dropThreshold.Value, reasons, reason);
    }
}
