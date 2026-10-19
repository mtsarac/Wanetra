using Wanetra.Application.Probes;
using Wanetra.Domain;

namespace Wanetra.Api.Contracts;

public static class WireNames
{
    public static string Of<T>(T value) where T : struct, Enum
    {
        var name = value.ToString();
        return char.ToLowerInvariant(name[0]) + name[1..];
    }
}

public sealed record ProbeTargetResponse(
    int Id,
    string Name,
    string Kind,
    string Role,
    string? Target,
    int? Port,
    string? Resolver,
    bool Enabled,
    DateTime CreatedAt,
    long? DeviceId)
{
    public static ProbeTargetResponse From(ProbeTarget target) => new(
        target.Id,
        target.Name,
        WireNames.Of(target.Kind),
        WireNames.Of(target.Role),
        target.Target,
        target.Port,
        target.Resolver,
        target.Enabled,
        target.CreatedAt,
        target.DeviceId);
}

public sealed record ProbeTargetRequest(
    string? Name,
    string? Kind,
    string? Role,
    string? Target,
    int? Port,
    string? Resolver,
    bool? Enabled)
{
    public ProbeTargetInput ToInput() => new(Name, Kind, Role, Target, Port, Resolver, Enabled ?? true);
}

public sealed record ProbeTargetStatusResponse(
    ProbeTargetResponse Target,
    bool? Up,
    double? RttMs,
    DateTime? CheckedAt,
    double? LossPercent,
    double? AvgRttMs,
    string? Error)
{
    public static ProbeTargetStatusResponse From(ProbeTargetStatus status) => new(
        ProbeTargetResponse.From(status.Target),
        status.Up,
        status.RttMs,
        status.CheckedAt,
        status.LossPercent,
        status.AvgRttMs,
        status.Error);
}

public sealed record LayersResponse(string Gateway, string Internet, string Dns)
{
    public static LayersResponse From(Diagnosis diagnosis) => new(
        WireNames.Of(diagnosis.Gateway),
        WireNames.Of(diagnosis.Internet),
        WireNames.Of(diagnosis.Dns));
}

public sealed record OutageResponse(
    long Id,
    DateTime StartedAt,
    DateTime? EndedAt,
    string Status,
    string Scope,
    string Reason,
    string? ClosureReason,
    double DurationSeconds,
    bool NotificationSent,
    bool RecoveryNotificationSent,
    IReadOnlyList<NotificationDeliveryResponse> NotificationDeliveries)
{
    public static OutageResponse From(Outage outage, DateTime now) => new(
        outage.Id,
        outage.StartedAt,
        outage.EndedAt,
        WireNames.Of(outage.Status),
        WireNames.Of(outage.Scope),
        outage.Reason,
        outage.ClosureReason,
        Math.Max(((outage.EndedAt ?? now) - outage.StartedAt).TotalSeconds, 0),
        outage.NotificationSent,
        outage.RecoveryNotificationSent,
        outage.NotificationDeliveries
            .OrderBy(delivery => delivery.Trigger)
            .ThenBy(delivery => delivery.ConfigurationId)
            .Select(NotificationDeliveryResponse.From)
            .ToList());
}

public sealed record ConnectivityStatusResponse(
    bool Monitoring,
    int IntervalSeconds,
    string State,
    LayersResponse Layers,
    string? Scope,
    string? Reason,
    DateTime? LastCheckedAt,
    IReadOnlyList<ProbeTargetStatusResponse> Targets,
    OutageResponse? ActiveOutage)
{
    public static ConnectivityStatusResponse From(ConnectivityStatus status, DateTime now) => new(
        status.Monitoring,
        status.IntervalSeconds,
        status.State,
        LayersResponse.From(status.Diagnosis),
        status.Diagnosis.Scope is { } scope ? WireNames.Of(scope) : null,
        status.Diagnosis.Scope is null ? null : status.Diagnosis.Reason,
        status.LastCheckedAt,
        status.Targets.Select(ProbeTargetStatusResponse.From).ToList(),
        status.ActiveOutage is null ? null : OutageResponse.From(status.ActiveOutage, now));
}

public sealed record ProbePointResponse(
    DateTime Timestamp,
    double? RttMs,
    double? RttMinMs,
    double? RttMaxMs,
    double? JitterMs,
    double LossPercent,
    int Sent);

public sealed record ProbeSeriesResponse(
    int TargetId,
    string Resolution,
    DateTime From,
    DateTime To,
    IReadOnlyList<ProbePointResponse> Points)
{
    public static ProbeSeriesResponse Create(ProbeSeries series) => new(
        series.TargetId,
        WireNames.Of(series.Resolution),
        series.From,
        series.To,
        series.Points
            .Select(point => new ProbePointResponse(
                point.Timestamp, point.RttMs, point.RttMinMs, point.RttMaxMs, point.JitterMs, point.LossPercent, point.Sent))
            .ToList());
}

public sealed record UptimeResponse(
    string Range,
    DateTime From,
    DateTime To,
    double UptimePercent,
    int OutageCount,
    double TotalOutageSeconds,
    double LongestOutageSeconds,
    bool CoversFullRange)
{
    public static UptimeResponse Create(string range, UptimeSummary summary) => new(
        range,
        summary.From,
        summary.To,
        summary.UptimePercent,
        summary.OutageCount,
        summary.TotalOutageSeconds,
        summary.LongestOutageSeconds,
        summary.CoversFullRange);
}
