using Wanetra.Api.Contracts;
using Wanetra.Application.Lan;
using Wanetra.Application.Settings;

namespace Wanetra.Api.Endpoints;

public static class LanEndpoints
{
    private const int DefaultEventCount = 50;
    private const int MaxEventCount = 200;

    public static IEndpointRouteBuilder MapLanEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/lan/status", async (DeviceService service, CancellationToken cancellationToken) =>
            Results.Ok(LanStatusResponse.From(await service.GetStatusAsync(cancellationToken))));

        endpoints.MapPost("/api/lan/scan", (
            LanScanCoordinator coordinator,
            SettingsSnapshot settings,
            IHostApplicationLifetime lifetime) =>
        {
            if (!settings.GetBool(SettingKeys.LanEnabled))
            {
                return Results.Conflict(new ApiError("lan_disabled", "Network scanning is turned off. Turn it on under Settings → Network scanning."));
            }

            return coordinator.TryStart("manual", lifetime.ApplicationStopping) is null
                ? Results.Conflict(new ApiError("scan_in_progress", "A scan is already running."))
                : Results.Accepted(value: new { status = "started" });
        });

        endpoints.MapGet("/api/lan/events", async (int? count, DeviceService service, CancellationToken cancellationToken) =>
            Results.Ok((await service.GetEventsAsync(null, Clamp(count), cancellationToken)).Select(DeviceEventResponse.From).ToList()));

        var devices = endpoints.MapGroup("/api/devices");

        devices.MapGet("", async (DeviceService service, CancellationToken cancellationToken) =>
            Results.Ok((await service.ListAsync(cancellationToken)).Select(DeviceResponse.From).ToList()));

        devices.MapPost("/mark-all-known", async (DeviceService service, CancellationToken cancellationToken) =>
            Results.Ok(new MarkKnownResponse(await service.MarkAllKnownAsync(cancellationToken))));

        devices.MapGet("/{id:long}", (long id, DeviceService service, CancellationToken cancellationToken) =>
            Run(async () => Results.Ok(DeviceResponse.From(await service.GetAsync(id, cancellationToken)))));

        devices.MapPut("/{id:long}", (long id, DeviceUpdateRequest? request, DeviceService service, CancellationToken cancellationToken) =>
            Run(async () =>
            {
                if (request is null)
                {
                    return Results.BadRequest(new ApiError("invalid_request", "A request body is required."));
                }

                var current = await service.GetAsync(id, cancellationToken);
                var updated = await service.UpdateAsync(
                    id,
                    new DeviceUpdate(request.Name ?? current.Name, request.Known ?? current.Known, request.Watch ?? current.Watch),
                    cancellationToken);
                return Results.Ok(DeviceResponse.From(updated));
            }));

        devices.MapDelete("/{id:long}", (long id, DeviceService service, CancellationToken cancellationToken) =>
            Run(async () =>
            {
                await service.DeleteAsync(id, cancellationToken);
                return Results.NoContent();
            }));

        devices.MapGet("/{id:long}/events", (long id, int? count, DeviceService service, CancellationToken cancellationToken) =>
            Run(async () =>
                Results.Ok((await service.GetEventsAsync(id, Clamp(count), cancellationToken)).Select(DeviceEventResponse.From).ToList())));

        return endpoints;
    }

    private static int Clamp(int? count) => Math.Clamp(count ?? DefaultEventCount, 1, MaxEventCount);

    private static async Task<IResult> Run(Func<Task<IResult>> action)
    {
        try
        {
            return await action();
        }
        catch (DeviceException exception)
        {
            return exception.Kind == DeviceErrorKind.NotFound
                ? Results.NotFound(new ApiError("device_not_found", exception.Message))
                : Results.BadRequest(new ApiError("invalid_request", exception.Message));
        }
    }
}
