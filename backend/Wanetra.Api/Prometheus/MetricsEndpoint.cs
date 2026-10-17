using Prometheus;

namespace Wanetra.Api.Prometheus;

public static class MetricsEndpoint
{
    // Serves the process-wide default registry (HTTP, runtime, EF Core) followed by Wanetra's own
    // per-instance registry. shortcut: plain-text exposition only, no OpenMetrics negotiation.
    public static IEndpointConventionBuilder MapWanetraMetrics(this IEndpointRouteBuilder app, string path, CollectorRegistry wanetraRegistry) =>
        app.MapGet(path, async (HttpContext context) =>
        {
            context.Response.ContentType = "text/plain; version=0.0.4; charset=utf-8";
            await Metrics.DefaultRegistry.CollectAndExportAsTextAsync(context.Response.Body, ExpositionFormat.PrometheusText, context.RequestAborted);
            await wanetraRegistry.CollectAndExportAsTextAsync(context.Response.Body, ExpositionFormat.PrometheusText, context.RequestAborted);
        });
}
