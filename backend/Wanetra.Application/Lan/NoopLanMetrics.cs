using Wanetra.Domain;

namespace Wanetra.Application.Lan;

internal sealed class NoopLanMetrics : ILanMetrics
{
    public void SetDeviceCounts(int total, int online, int unknown) { }

    public void RecordScan(bool success, double durationSeconds) { }
}
