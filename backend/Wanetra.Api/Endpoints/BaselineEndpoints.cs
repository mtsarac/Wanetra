using Wanetra.Application.Baselines;

namespace Wanetra.Api.Endpoints;

public static class BaselineEndpoints
{
    public static IEndpointRouteBuilder MapBaselineEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/baseline", async (
            BaselineService service,
            CancellationToken cancellationToken) =>
            Results.Ok(await service.GetAsync(cancellationToken)));

        return endpoints;
    }
}
