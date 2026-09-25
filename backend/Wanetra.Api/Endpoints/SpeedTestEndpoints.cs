using Wanetra.Api.Contracts;
using Wanetra.Application.SpeedTests;
using Wanetra.Domain;

namespace Wanetra.Api.Endpoints;

public static class SpeedTestEndpoints
{
    private const int DefaultPageSize = 50;
    private const int MaxPageSize = 200;

    public static IEndpointRouteBuilder MapSpeedTestEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var speedTests = endpoints.MapGroup("/api/speedtests");

        speedTests.MapPost("/run", Run);
        speedTests.MapGet("/status", GetStatus);
        speedTests.MapGet("/latest", GetLatest);
        speedTests.MapGet("/{id:long}", GetById);
        speedTests.MapGet("", GetHistory);

        return endpoints;
    }

    private static IResult Run(SpeedTestCoordinator coordinator, IHostApplicationLifetime lifetime)
    {
        if (coordinator.TryStart(SpeedTestTrigger.Manual, lifetime.ApplicationStopping) is null)
        {
            return Results.Conflict(new ApiError(
                "speedtest_already_running",
                "A speed test is already running."));
        }

        return Results.Accepted(value: new SpeedTestRunResponse("started"));
    }

    private static IResult GetStatus(SpeedTestCoordinator coordinator) =>
        Results.Ok(SpeedTestStatusResponse.From(coordinator.Status));

    private static async Task<IResult> GetLatest(
        ISpeedTestResultRepository repository,
        CancellationToken cancellationToken)
    {
        var result = await repository.FindLatestAsync(cancellationToken);

        return result is null
            ? Results.NotFound(new ApiError("speedtest_not_found", "No speed test has been recorded yet."))
            : Results.Ok(SpeedTestResultResponse.From(result));
    }

    private static async Task<IResult> GetById(
        long id,
        ISpeedTestResultRepository repository,
        CancellationToken cancellationToken)
    {
        var result = await repository.FindAsync(id, cancellationToken);

        return result is null
            ? Results.NotFound(new ApiError("speedtest_not_found", $"No speed test with id {id}."))
            : Results.Ok(SpeedTestResultResponse.From(result));
    }

    private static async Task<IResult> GetHistory(
        ISpeedTestResultRepository repository,
        CancellationToken cancellationToken,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        bool? success = null,
        string? engine = null,
        string? sort = null,
        int page = 1,
        int pageSize = DefaultPageSize)
    {
        if (page < 1)
        {
            return Invalid("page must be 1 or greater.");
        }

        if (pageSize is < 1 || pageSize > MaxPageSize)
        {
            return Invalid($"pageSize must be between 1 and {MaxPageSize}.");
        }

        if (from > to)
        {
            return Invalid("from must not be later than to.");
        }

        if (!TryReadSort(sort, out var newestFirst))
        {
            return Invalid("sort must be 'asc' or 'desc'.");
        }

        var results = await repository.QueryAsync(
            new SpeedTestResultQuery
            {
                From = from?.UtcDateTime,
                To = to?.UtcDateTime,
                Success = success,

                Engine = string.IsNullOrWhiteSpace(engine) ? null : engine.Trim().ToLowerInvariant(),
                NewestFirst = newestFirst,
                Page = page,
                PageSize = pageSize,
            },
            cancellationToken);

        return Results.Ok(SpeedTestHistoryResponse.From(results));
    }

    private static bool TryReadSort(string? sort, out bool newestFirst)
    {
        newestFirst = !string.Equals(sort, "asc", StringComparison.OrdinalIgnoreCase);

        return string.IsNullOrWhiteSpace(sort)
            || string.Equals(sort, "asc", StringComparison.OrdinalIgnoreCase)
            || string.Equals(sort, "desc", StringComparison.OrdinalIgnoreCase);
    }

    private static IResult Invalid(string message) =>
        Results.BadRequest(new ApiError("invalid_request", message));
}
