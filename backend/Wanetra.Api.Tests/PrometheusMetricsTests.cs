using System.Net;

namespace Wanetra.Api.Tests;

public class PrometheusMetricsTests(WanetraApiFactory factory) : IClassFixture<WanetraApiFactory>
{
    [Fact]
    public async Task Metrics_endpoint_exposes_stable_speed_test_metric_names()
    {
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/metrics");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("wanetra_download_mbps", body);
        Assert.Contains("wanetra_speedtests_total", body);
        Assert.Contains("wanetra_speedtest_failures_total", body);
        Assert.Contains("wanetra_connection_degraded", body);
        Assert.Contains("wanetra_packet_loss_percent NaN", body);
    }
}
