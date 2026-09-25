namespace Wanetra.Domain;

public interface IPrometheusMetrics
{
    void Initialize(SpeedTestResult? latestResult, SpeedTestResult? latestSuccessfulResult, bool connectionDegraded);
    void RecordSpeedTest(SpeedTestResult result);
    void SetConnectionDegraded(bool degraded);
}
