using Wanetra.Api.Contracts;
using Wanetra.Application.Scheduling;

namespace Wanetra.Api.Endpoints;

public static class ScheduleEndpoints
{
    private const int DefaultNextRuns = 5;

    public static IEndpointRouteBuilder MapScheduleEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var schedule = endpoints.MapGroup("/api/schedule");

        schedule.MapGet("", Get);
        schedule.MapPut("", Update);
        schedule.MapGet("/next-runs", GetNextRuns);

        return endpoints;
    }

    private static async Task<IResult> Get(
        ScheduleService service,
        CancellationToken cancellationToken)
    {
        var settings = await service.GetAsync(cancellationToken);
        IReadOnlyList<DateTime> runs;
        try
        {
            runs = await service.GetNextRunsAsync(DefaultNextRuns, cancellationToken);
        }
        catch (ArgumentException ex)
        {
            return Invalid(ex.Message);
        }

        return Results.Ok(ScheduleResponse.From(settings, runs));
    }

    private static async Task<IResult> Update(
        ScheduleUpdateRequest request,
        ScheduleService service,
        CancellationToken cancellationToken)
    {
        ScheduleResponse response;
        try
        {
            var settings = await service.UpdateAsync(
                request.Enabled,
                request.CronExpression,
                request.Timezone,
                cancellationToken);
            var runs = await service.GetNextRunsAsync(DefaultNextRuns, cancellationToken);
            response = ScheduleResponse.From(settings, runs);
        }
        catch (ArgumentException ex)
        {
            return Invalid(ex.Message);
        }

        return Results.Ok(response);
    }

    private static async Task<IResult> GetNextRuns(
        ScheduleService service,
        CancellationToken cancellationToken,
        int count = DefaultNextRuns)
    {
        IReadOnlyList<DateTime> runs;
        try
        {
            runs = await service.GetNextRunsAsync(count, cancellationToken);
        }
        catch (ArgumentOutOfRangeException)
        {
            return Invalid($"count must be between 1 and {ScheduleCalculator.MaxNextRuns}.");
        }
        catch (ArgumentException ex)
        {
            return Invalid(ex.Message);
        }

        return Results.Ok(new ScheduleNextRunsResponse(runs));
    }

    private static IResult Invalid(string message) =>
        Results.BadRequest(new ApiError("invalid_request", message));
}
