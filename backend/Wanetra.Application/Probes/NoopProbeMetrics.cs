using Wanetra.Domain;

namespace Wanetra.Application.Probes;

internal sealed class NoopProbeMetrics : IProbeMetrics
{
    public void RecordProbe(string target, string role, bool success, double? rttMs) { }

    public void RemoveProbeTarget(string target) { }

    public void SetOutageActive(bool active) { }

    public void RecordOutageOpened() { }
}
