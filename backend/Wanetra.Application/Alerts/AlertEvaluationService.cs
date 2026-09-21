using Wanetra.Application.Baselines;
using Wanetra.Domain;

namespace Wanetra.Application.Alerts;

public sealed class AlertEvaluationService(
    IAlertStateRepository repository,
    BaselineService baselineService,
    TimeProvider timeProvider)
{
    public async Task EvaluateAsync(SpeedTestResult result, CancellationToken cancellationToken)
    {
        if (!result.Success)
        {
            return;
        }

        var rule = await repository.GetEnabledRuleAsync(cancellationToken);
        if (rule is null || rule.ConsecutiveFailuresRequired < 1 || rule.ConsecutiveRecoveriesRequired < 1)
        {
            return;
        }

        var state = await repository.GetStateAsync(cancellationToken);
        ResetPendingOnRuleChange(state, rule);

        var baseline = await baselineService.GetAsync(cancellationToken);
        var evaluation = AlertConditionEvaluator.Evaluate(rule, result, baseline);
        if (!evaluation.Evaluated)
        {
            return;
        }

        var openEvent = await repository.GetOpenEventAsync(cancellationToken);
        if (openEvent is null)
        {
            EvaluateWithoutOpenEvent(result, rule, state, evaluation, baseline);
        }
        else
        {
            EvaluateOpenEvent(result, rule, state, openEvent, evaluation);
        }

        state.UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;
        await repository.SaveChangesAsync(cancellationToken);
    }

    private void EvaluateWithoutOpenEvent(
        SpeedTestResult result,
        AlertRule rule,
        AlertState state,
        AlertEvaluationResult evaluation,
        BaselineSnapshot baseline)
    {
        if (!evaluation.Unhealthy)
        {
            state.ConsecutiveUnhealthyMeasurements = 0;
            state.ConsecutiveHealthyMeasurements = 0;
            return;
        }

        state.ConsecutiveUnhealthyMeasurements++;
        state.ConsecutiveHealthyMeasurements = 0;
        if (state.ConsecutiveUnhealthyMeasurements < rule.ConsecutiveFailuresRequired)
        {
            return;
        }

        repository.AddEvent(new DegradationEvent
        {
            StartedAt = result.Timestamp,
            Status = DegradationStatus.Active,
            Reason = evaluation.Reason,
            BaselineDownloadMbps = baseline.Download.BaselineMbps,
            WorstDownloadMbps = result.DownloadMbps,
            BaselineUploadMbps = baseline.Upload.BaselineMbps,
            WorstUploadMbps = result.UploadMbps,
            MaxLatencyMs = result.LatencyMs,
            MaxJitterMs = result.JitterMs,
            MaxPacketLossPercent = result.PacketLossPercent,
            ConsecutiveUnhealthyMeasurements = state.ConsecutiveUnhealthyMeasurements,
        });
        state.ConsecutiveUnhealthyMeasurements = 0;
    }

    private static void EvaluateOpenEvent(
        SpeedTestResult result,
        AlertRule rule,
        AlertState state,
        DegradationEvent openEvent,
        AlertEvaluationResult evaluation)
    {
        UpdateWorstMetrics(openEvent, result);
        if (evaluation.Unhealthy)
        {
            openEvent.Status = DegradationStatus.Active;
            openEvent.ConsecutiveHealthyMeasurements = 0;
            openEvent.ConsecutiveUnhealthyMeasurements++;
            state.ConsecutiveHealthyMeasurements = 0;
            state.ConsecutiveUnhealthyMeasurements = 0;
            return;
        }

        openEvent.Status = DegradationStatus.Recovering;
        openEvent.ConsecutiveHealthyMeasurements++;
        openEvent.ConsecutiveUnhealthyMeasurements = 0;
        state.ConsecutiveHealthyMeasurements = openEvent.ConsecutiveHealthyMeasurements;
        state.ConsecutiveUnhealthyMeasurements = 0;
        if (openEvent.ConsecutiveHealthyMeasurements < rule.ConsecutiveRecoveriesRequired)
        {
            return;
        }

        openEvent.Status = DegradationStatus.Recovered;
        openEvent.EndedAt = result.Timestamp;
        state.ConsecutiveHealthyMeasurements = 0;
    }

    private static void ResetPendingOnRuleChange(AlertState state, AlertRule rule)
    {
        if (state.RuleId == rule.Id && state.RuleUpdatedAt == rule.UpdatedAt)
        {
            return;
        }

        state.RuleId = rule.Id;
        state.RuleUpdatedAt = rule.UpdatedAt;
        state.ConsecutiveHealthyMeasurements = 0;
        state.ConsecutiveUnhealthyMeasurements = 0;
    }

    private static void UpdateWorstMetrics(DegradationEvent openEvent, SpeedTestResult result)
    {
        openEvent.WorstDownloadMbps = Minimum(openEvent.WorstDownloadMbps, result.DownloadMbps);
        openEvent.WorstUploadMbps = Minimum(openEvent.WorstUploadMbps, result.UploadMbps);
        openEvent.MaxLatencyMs = Maximum(openEvent.MaxLatencyMs, result.LatencyMs);
        openEvent.MaxJitterMs = Maximum(openEvent.MaxJitterMs, result.JitterMs);
        openEvent.MaxPacketLossPercent = Maximum(openEvent.MaxPacketLossPercent, result.PacketLossPercent);
    }

    private static double? Minimum(double? current, double? candidate) =>
        current.HasValue && candidate.HasValue ? Math.Min(current.Value, candidate.Value) : current ?? candidate;

    private static double? Maximum(double? current, double? candidate) =>
        current.HasValue && candidate.HasValue ? Math.Max(current.Value, candidate.Value) : current ?? candidate;
}
