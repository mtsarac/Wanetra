using Wanetra.Domain;

namespace Wanetra.Application.Probes;

public sealed record ProbeRequest(ProbeKind Kind, string Host, int? Port, string? Resolver, TimeSpan Timeout);

public sealed record ProbeOutcome(bool Success, double? RttMs, string? Error)
{
    public static ProbeOutcome Up(double rttMs) => new(true, rttMs, null);

    public static ProbeOutcome Down(string error) => new(false, null, error);
}

public interface IProbeExecutor
{
    Task<ProbeOutcome> ExecuteAsync(ProbeRequest request, CancellationToken cancellationToken);
}

public interface IGatewayDetector
{
    string? DetectGateway();
}

public sealed record ProbeTargetResult(ProbeTarget Target, ProbeOutcome Outcome);

public sealed record OutageTransition(NotificationTrigger Trigger, long OutageId);

public static class ProbeInternalKeys
{
    public const string MonitoringSince = "internal.probes.monitoringSince";
    public const string DefaultsSeeded = "internal.probes.defaultsSeeded";
}
