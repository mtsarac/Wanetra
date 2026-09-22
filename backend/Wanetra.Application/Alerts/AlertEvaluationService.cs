using Wanetra.Application.Baselines;
using Wanetra.Application.Notifications;
using Wanetra.Domain;

namespace Wanetra.Application.Alerts;

public sealed class AlertEvaluationService(
    IAlertStateRepository repository,
    BaselineService baselineService,
    TimeProvider timeProvider)
{
    public async Task<(NotificationTrigger? Trigger, long? EventId)> EvaluateAsync(
        SpeedTestResult result, CancellationToken cancellationToken)
    {
        if (!result.Success)
        {
            return (null, null);
        }

        var rule = await repository.GetEnabledRuleAsync(cancellationToken);
        if (rule is null || rule.ConsecutiveFailuresRequired < 1 || rule.ConsecutiveRecoveriesRequired < 1)
        {
            return (null, null);
        }

        var state = await repository.GetStateAsync(cancellationToken);
        ResetPendingOnRuleChange(state, rule);

        var baseline = await baselineService.GetAsync(cancellationToken);
        var evaluation = AlertConditionEvaluator.Evaluate(rule, result, baseline);
        if (!evaluation.Evaluated)
        {
            return (null, null);
        }

        DegradationEvent? openedEvent = null;
        var recovered = false;
        var openEvent = await repository.GetOpenEventAsync(cancellationToken);
        if (openEvent is null)
        {
            openedEvent = EvaluateWithoutOpenEvent(result, rule, state, evaluation, baseline);
        }
        else
        {
            recovered = EvaluateOpenEvent(result, rule, state, openEvent, evaluation);
        }

        state.UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;
        await repository.SaveChangesAsync(cancellationToken);

        if (openedEvent is not null)
        {
            return (NotificationTrigger.Opened, openedEvent.Id);
        }

        return recovered && openEvent is not null
            ? (NotificationTrigger.Recovered, openEvent.Id)
            : (null, null);
    }

    private DegradationEvent? EvaluateWithoutOpenEvent(
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
            return null;
        }

        state.ConsecutiveUnhealthyMeasurements++;
        state.ConsecutiveHealthyMeasurements = 0;
        if (state.ConsecutiveUnhealthyMeasurements < rule.ConsecutiveFailuresRequired)
        {
            return null;
        }

        var degradationEvent = new DegradationEvent
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
        };
        repository.AddEvent(degradationEvent);
        state.ConsecutiveUnhealthyMeasurements = 0;
        return degradationEvent;
    }

    private static bool EvaluateOpenEvent(
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
            return false;
        }

        openEvent.Status = DegradationStatus.Recovering;
        openEvent.ConsecutiveHealthyMeasurements++;
        openEvent.ConsecutiveUnhealthyMeasurements = 0;
        state.ConsecutiveHealthyMeasurements = openEvent.ConsecutiveHealthyMeasurements;
        state.ConsecutiveUnhealthyMeasurements = 0;
        if (openEvent.ConsecutiveHealthyMeasurements < rule.ConsecutiveRecoveriesRequired)
        {
            return false;
        }

        openEvent.Status = DegradationStatus.Recovered;
        openEvent.EndedAt = result.Timestamp;
        state.ConsecutiveHealthyMeasurements = 0;
        return true;
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
