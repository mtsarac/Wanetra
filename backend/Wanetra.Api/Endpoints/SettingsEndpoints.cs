using Microsoft.Extensions.Options;
using Wanetra.Api.Contracts;
using Wanetra.Application.Maintenance;

namespace Wanetra.Api.Endpoints;

public static class SettingsEndpoints
{
    public static IEndpointRouteBuilder MapSettingsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/settings/retention", (IOptions<DataRetentionOptions> options) =>
            Results.Ok(new RetentionSettingsResponse(options.Value.Days)));

        return endpoints;
    }
}
