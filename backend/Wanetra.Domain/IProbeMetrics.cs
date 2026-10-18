namespace Wanetra.Domain;

public interface IProbeMetrics
{
    void RecordProbe(string target, string role, bool success, double? rttMs);
    void RemoveProbeTarget(string target);
    void SetOutageActive(bool active);
    void RecordOutageOpened();
}
