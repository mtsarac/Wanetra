using Wanetra.Api.Contracts;
using Wanetra.Domain;

namespace Wanetra.Api.Endpoints;

public static class AlertEndpoints
{
    public static IEndpointRouteBuilder MapAlertEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/alerts/active", async (
            IAlertStateRepository repository,
            CancellationToken cancellationToken) =>
        {
            var degradationEvent = await repository.GetOpenEventAsync(cancellationToken);
            return degradationEvent is null
                ? Results.NotFound()
                : Results.Ok(ActiveAlertResponse.From(degradationEvent));
        });

        return endpoints;
    }
}
