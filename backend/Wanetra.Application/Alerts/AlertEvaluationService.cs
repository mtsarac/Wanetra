using Wanetra.Application.Baselines;
using Wanetra.Application.Notifications;
using Wanetra.Domain;

namespace Wanetra.Application.Alerts;

public sealed class AlertEvaluationService(
    IAlertStateRepository repository,
    INotificationConfigurationRepository notificationConfigurationRepository,
    BaselineService baselineService,
    IPrometheusMetrics metrics,
    TimeProvider timeProvider)
{
    public async Task SynchronizeRuleStateAsync(AlertRule? rule, CancellationToken cancellationToken)
    {
        var state = await repository.GetStateAsync(cancellationToken);
        var openEvent = await repository.GetOpenEventAsync(cancellationToken);
        var changed = state.RuleId != rule?.Id || state.RuleUpdatedAt != rule?.UpdatedAt;
        if (changed)
        {
            state.RuleId = rule?.Id;
            state.RuleUpdatedAt = rule?.UpdatedAt;
            state.ConsecutiveHealthyMeasurements = 0;
            state.ConsecutiveUnhealthyMeasurements = 0;
        }

        if (openEvent is not null)
        {
            if (rule?.Enabled != true)
            {
                openEvent.Status = DegradationStatus.Disabled;
                openEvent.EndedAt ??= timeProvider.GetUtcNow().UtcDateTime;
                openEvent.ClosureReason = "Alert rule disabled";
                openEvent.ConsecutiveHealthyMeasurements = 0;
                openEvent.ConsecutiveUnhealthyMeasurements = 0;
            }
            else if (changed)
            {
                openEvent.Status = DegradationStatus.Active;
                openEvent.ConsecutiveHealthyMeasurements = 0;
                openEvent.ConsecutiveUnhealthyMeasurements = 0;
            }
        }

        state.UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;
        await repository.SaveChangesAsync(cancellationToken);
        metrics.SetConnectionDegraded(rule?.Enabled == true && openEvent is not null);
    }

    public async Task<(NotificationTrigger? Trigger, long? EventId)> EvaluateAsync(
        SpeedTestResult result, CancellationToken cancellationToken)
    {
        var rule = await repository.GetRuleAsync(cancellationToken);
        if (rule is null || !rule.Enabled || rule.ConsecutiveFailuresRequired < 1 || rule.ConsecutiveRecoveriesRequired < 1)
        {
            await SynchronizeRuleStateAsync(rule, cancellationToken);
            return (null, null);
        }

        var state = await repository.GetStateAsync(cancellationToken);
        var ruleChanged = ResetPendingOnRuleChange(state, rule);
        var openEvent = await repository.GetOpenEventAsync(cancellationToken);
        if (ruleChanged && openEvent is not null)
        {
            openEvent.Status = DegradationStatus.Active;
            openEvent.ConsecutiveHealthyMeasurements = 0;
            openEvent.ConsecutiveUnhealthyMeasurements = 0;
        }

        if (!result.Success
            && result.FailureKind is not (SpeedTestFailureKind.NetworkFailure or SpeedTestFailureKind.MeasurementFailure))
        {
            if (ruleChanged)
            {
                state.UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;
                await repository.SaveChangesAsync(cancellationToken);
                metrics.SetConnectionDegraded(openEvent is not null);
            }

            return (null, null);
        }
        var baseline = await baselineService.GetForMeasurementAsync(result.Timestamp, cancellationToken);
        var evaluation = AlertConditionEvaluator.Evaluate(rule, result, baseline);
        if (evaluation.Outcome == AlertMeasurementOutcome.Ignored)
        {
            if (ruleChanged)
            {
                state.UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;
                await repository.SaveChangesAsync(cancellationToken);
                metrics.SetConnectionDegraded(openEvent is not null);
            }

            return (null, null);
        }

        DegradationEvent? openedEvent = null;
        var recovered = false;

        if (openEvent is null)
        {
            openedEvent = EvaluateWithoutOpenEvent(result, rule, state, evaluation, baseline);
        }
        else
        {
            recovered = EvaluateOpenEvent(result, rule, state, openEvent, evaluation);
        }

        if (openedEvent is not null)
        {
            await InitializeDeliverySnapshotAsync(openedEvent, NotificationTrigger.Opened, cancellationToken);
        }
        else if (recovered && openEvent is not null)
        {
            await InitializeDeliverySnapshotAsync(openEvent, NotificationTrigger.Recovered, cancellationToken);
        }

        state.UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;
        await repository.SaveChangesAsync(cancellationToken);
        metrics.SetConnectionDegraded(await repository.GetOpenEventAsync(cancellationToken) is not null);

        if (openedEvent is not null)
        {
            return (NotificationTrigger.Opened, openedEvent.Id);
        }

        return recovered && openEvent is not null
            ? (NotificationTrigger.Recovered, openEvent.Id)
            : (null, null);
    }
    private async Task InitializeDeliverySnapshotAsync(
        DegradationEvent degradationEvent,
        NotificationTrigger trigger,
        CancellationToken cancellationToken)
    {
        if (trigger == NotificationTrigger.Opened)
        {
            if (degradationEvent.OpenedDeliveryInitialized)
            {
                return;
            }

            var configurations = await notificationConfigurationRepository.ListAsync(cancellationToken);
            foreach (var configuration in configurations.Where(configuration => configuration.Enabled).OrderBy(configuration => configuration.Id))
            {
                repository.AddNotificationDelivery(new NotificationDelivery
                {
                    DegradationEvent = degradationEvent,
                    DegradationEventId = degradationEvent.Id,
                    Trigger = NotificationTrigger.Opened,
                    ConfigurationId = configuration.Id,
                    Provider = configuration.Provider,
                    Status = NotificationDeliveryStatus.Pending,
                });
            }

            degradationEvent.OpenedDeliveryInitialized = true;
            return;
        }

        if (degradationEvent.RecoveryDeliveryInitialized)
        {
            return;
        }

        foreach (var openedDelivery in degradationEvent.NotificationDeliveries
                     .Where(delivery => delivery.Trigger == NotificationTrigger.Opened)
                     .OrderBy(delivery => delivery.ConfigurationId))
        {
            repository.AddNotificationDelivery(new NotificationDelivery
            {
                DegradationEvent = degradationEvent,
                DegradationEventId = degradationEvent.Id,
                Trigger = NotificationTrigger.Recovered,
                ConfigurationId = openedDelivery.ConfigurationId,
                Provider = openedDelivery.Provider,
                Status = NotificationDeliveryStatus.Pending,
            });
        }

        degradationEvent.RecoveryDeliveryInitialized = true;
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
        if (evaluation.Unhealthy)
        {
            UpdateWorstMetrics(openEvent, result);
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
    private static bool ResetPendingOnRuleChange(AlertState state, AlertRule rule)
    {
        if (state.RuleId == rule.Id && state.RuleUpdatedAt == rule.UpdatedAt)
        {
            return false;
        }

        state.RuleId = rule.Id;
        state.RuleUpdatedAt = rule.UpdatedAt;
        state.ConsecutiveHealthyMeasurements = 0;
        state.ConsecutiveUnhealthyMeasurements = 0;
        return true;
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
