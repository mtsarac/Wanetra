using Wanetra.Domain;

namespace Wanetra.Application.SpeedTests;

internal sealed class NoopPrometheusMetrics : IPrometheusMetrics
{
    public void Initialize(SpeedTestResult? latestResult, SpeedTestResult? latestSuccessfulResult, bool connectionDegraded) { }
    public void RecordSpeedTest(SpeedTestResult result) { }

    public void SetConnectionDegraded(bool degraded) { }
}
