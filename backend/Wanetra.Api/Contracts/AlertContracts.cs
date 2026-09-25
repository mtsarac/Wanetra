using Wanetra.Domain;

namespace Wanetra.Api.Contracts;

public sealed record AlertRuleResponse(
    int Id,
    string Name,
    bool Enabled,
    double? MinDownloadMbps,
    double? MinUploadMbps,
    double? MaxLatencyMs,
    double? MaxJitterMs,
    double? MaxPacketLossPercent,
    double? DownloadBaselineDropPercent,
    double? UploadBaselineDropPercent,
    int ConsecutiveFailuresRequired,
    int ConsecutiveRecoveriesRequired,
    DateTime UpdatedAt)
{
    public static AlertRuleResponse From(AlertRule rule) => new(
        rule.Id,
        rule.Name,
        rule.Enabled,
        rule.MinDownloadMbps,
        rule.MinUploadMbps,
        rule.MaxLatencyMs,
        rule.MaxJitterMs,
        rule.MaxPacketLossPercent,
        rule.DownloadBaselineDropPercent,
        rule.UploadBaselineDropPercent,
        rule.ConsecutiveFailuresRequired,
        rule.ConsecutiveRecoveriesRequired,
        rule.UpdatedAt);
}

public sealed record AlertRuleUpdateRequest(
    string Name,
    bool Enabled,
    double? MinDownloadMbps,
    double? MinUploadMbps,
    double? MaxLatencyMs,
    double? MaxJitterMs,
    double? MaxPacketLossPercent,
    double? DownloadBaselineDropPercent,
    double? UploadBaselineDropPercent,
    int ConsecutiveFailuresRequired,
    int ConsecutiveRecoveriesRequired);

public sealed record DegradationEventResponse(
    long Id,
    DateTime StartedAt,
    DateTime? EndedAt,
    string Status,
    string Reason,
    double? BaselineDownloadMbps,
    double? WorstDownloadMbps,
    double? BaselineUploadMbps,
    double? WorstUploadMbps,
    double? MaxLatencyMs,
    double? MaxJitterMs,
    double? MaxPacketLossPercent,
    bool NotificationSent,
    bool RecoveryNotificationSent)
{
    public static DegradationEventResponse From(DegradationEvent degradationEvent) => new(
        degradationEvent.Id,
        degradationEvent.StartedAt,
        degradationEvent.EndedAt,
        degradationEvent.Status.ToString().ToLowerInvariant(),
        degradationEvent.Reason,
        degradationEvent.BaselineDownloadMbps,
        degradationEvent.WorstDownloadMbps,
        degradationEvent.BaselineUploadMbps,
        degradationEvent.WorstUploadMbps,
        degradationEvent.MaxLatencyMs,
        degradationEvent.MaxJitterMs,
        degradationEvent.MaxPacketLossPercent,
        degradationEvent.NotificationSent,
        degradationEvent.RecoveryNotificationSent);
}

public sealed record ActiveAlertResponse(
    long Id,
    DateTime StartedAt,
    string Status,
    string Reason,
    double? BaselineDownloadMbps,
    double? WorstDownloadMbps,
    double? BaselineUploadMbps,
    double? WorstUploadMbps)
{
    public static ActiveAlertResponse From(DegradationEvent degradationEvent) => new(
        degradationEvent.Id,
        degradationEvent.StartedAt,
        degradationEvent.Status.ToString().ToLowerInvariant(),
        degradationEvent.Reason,
        degradationEvent.BaselineDownloadMbps,
        degradationEvent.WorstDownloadMbps,
        degradationEvent.BaselineUploadMbps,
        degradationEvent.WorstUploadMbps);
}
