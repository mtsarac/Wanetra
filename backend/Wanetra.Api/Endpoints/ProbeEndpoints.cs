using Wanetra.Api.Contracts;
using Wanetra.Application.Probes;
using Wanetra.Domain;

namespace Wanetra.Api.Endpoints;

public static class ProbeEndpoints
{
    private const int DefaultOutageCount = 20;
    private const int MaxOutageCount = 100;

    private static readonly Dictionary<string, TimeSpan> UptimeRanges = new(StringComparer.OrdinalIgnoreCase)
    {
        ["24h"] = TimeSpan.FromHours(24),
        ["7d"] = TimeSpan.FromDays(7),
        ["30d"] = TimeSpan.FromDays(30),
        ["90d"] = TimeSpan.FromDays(90),
    };

    public static IEndpointRouteBuilder MapProbeEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var probes = endpoints.MapGroup("/api/probes");

        probes.MapGet("/targets", async (ProbeTargetService service, CancellationToken cancellationToken) =>
            Results.Ok((await service.ListAsync(cancellationToken)).Select(ProbeTargetResponse.From).ToList()));

        probes.MapPost("/targets", (ProbeTargetRequest? request, ProbeTargetService service, CancellationToken cancellationToken) =>
            RunTargetCommand(async () =>
            {
                var created = await service.CreateAsync(Require(request).ToInput(), cancellationToken);
                return Results.Created($"/api/probes/targets/{created.Id}", ProbeTargetResponse.From(created));
            }));

        probes.MapPut("/targets/{id:int}", (int id, ProbeTargetRequest? request, ProbeTargetService service, CancellationToken cancellationToken) =>
            RunTargetCommand(async () =>
                Results.Ok(ProbeTargetResponse.From(await service.UpdateAsync(id, Require(request).ToInput(), cancellationToken)))));

        probes.MapDelete("/targets/{id:int}", (int id, ProbeTargetService service, CancellationToken cancellationToken) =>
            RunTargetCommand(async () =>
            {
                await service.DeleteAsync(id, cancellationToken);
                return Results.NoContent();
            }));

        probes.MapGet("/status", async (ProbeQueryService query, TimeProvider time, CancellationToken cancellationToken) =>
            Results.Ok(ConnectivityStatusResponse.From(await query.GetStatusAsync(cancellationToken), time.GetUtcNow().UtcDateTime)));

        probes.MapGet("/series", async (
            int? targetId,
            DateTime? from,
            DateTime? to,
            string? resolution,
            ProbeQueryService query,
            TimeProvider time,
            CancellationToken cancellationToken) =>
        {
            if (targetId is null)
            {
                return Results.BadRequest(new ApiError("invalid_request", "targetId is required."));
            }

            if (!TryParseResolution(resolution, out var parsed))
            {
                return Results.BadRequest(new ApiError("invalid_request", "resolution must be auto, raw, minute or hour."));
            }

            var end = ToUtc(to) ?? time.GetUtcNow().UtcDateTime;
            var start = ToUtc(from) ?? end.AddHours(-1);
            try
            {
                return Results.Ok(ProbeSeriesResponse.Create(
                    await query.GetSeriesAsync(targetId.Value, start, end, parsed, cancellationToken)));
            }
            catch (SeriesException exception)
            {
                return Results.BadRequest(new ApiError("invalid_request", exception.Message));
            }
        });

        endpoints.MapGet("/api/outages", async (
            int? count,
            IOutageRepository outages,
            TimeProvider time,
            CancellationToken cancellationToken) =>
        {
            var take = Math.Clamp(count ?? DefaultOutageCount, 1, MaxOutageCount);
            var now = time.GetUtcNow().UtcDateTime;
            return Results.Ok((await outages.GetRecentOutagesAsync(take, cancellationToken))
                .Select(outage => OutageResponse.From(outage, now))
                .ToList());
        });

        endpoints.MapGet("/api/uptime", async (string? range, ProbeQueryService query, CancellationToken cancellationToken) =>
        {
            var key = range ?? "24h";
            if (!UptimeRanges.TryGetValue(key, out var window))
            {
                return Results.BadRequest(new ApiError("invalid_request", "range must be 24h, 7d, 30d or 90d."));
            }

            return Results.Ok(UptimeResponse.Create(key.ToLowerInvariant(), await query.GetUptimeAsync(window, cancellationToken)));
        });

        return endpoints;
    }

    private static ProbeTargetRequest Require(ProbeTargetRequest? request) =>
        request ?? throw new ProbeTargetException(ProbeTargetErrorKind.Invalid, "A request body is required.");

    private static async Task<IResult> RunTargetCommand(Func<Task<IResult>> command)
    {
        try
        {
            return await command();
        }
        catch (ProbeTargetException exception)
        {
            return exception.Kind switch
            {
                ProbeTargetErrorKind.NotFound => Results.NotFound(new ApiError("probe_target_not_found", exception.Message)),
                ProbeTargetErrorKind.Duplicate => Results.Conflict(new ApiError("probe_target_duplicate", exception.Message)),
                ProbeTargetErrorKind.LimitReached => Results.Conflict(new ApiError("probe_target_limit", exception.Message)),
                _ => Results.BadRequest(new ApiError("invalid_request", exception.Message)),
            };
        }
    }

    private static bool TryParseResolution(string? value, out SeriesResolution resolution)
    {
        resolution = SeriesResolution.Auto;
        return string.IsNullOrEmpty(value)
            || (Enum.TryParse(value, ignoreCase: true, out resolution) && Enum.IsDefined(resolution));
    }

    private static DateTime? ToUtc(DateTime? value) => value?.Kind switch
    {
        null => null,
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.Value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value.Value, DateTimeKind.Utc),
    };
}
