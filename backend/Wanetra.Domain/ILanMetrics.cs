namespace Wanetra.Domain;

public interface ILanMetrics
{
    void SetDeviceCounts(int total, int online, int unknown);
    void RecordScan(bool success, double durationSeconds);
}
