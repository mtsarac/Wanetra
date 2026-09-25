using Wanetra.Api.Contracts;
using Wanetra.Application.Alerts;
using Wanetra.Domain;

namespace Wanetra.Api.Endpoints;

public static class AlertEndpoints
{
    public static IEndpointRouteBuilder MapAlertEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/alerts/rule", async (
            IAlertStateRepository repository,
            CancellationToken cancellationToken) =>
        {
            var rule = await repository.GetRuleAsync(cancellationToken);
            return rule is null ? Results.NotFound() : Results.Ok(AlertRuleResponse.From(rule));
        });

        endpoints.MapPut("/api/alerts/rule", async (
            AlertRuleUpdateRequest request,
            IAlertStateRepository repository,
            AlertEvaluationService alertEvaluationService,
            TimeProvider timeProvider,
            CancellationToken cancellationToken) =>
        {
            if (!IsValid(request))
            {
                return Results.BadRequest(new ApiError(
                    "invalid_request",
                    "Thresholds must be non-negative, percentages must be between 0 and 100, and consecutive counts must be at least 1."));
            }

            var now = timeProvider.GetUtcNow().UtcDateTime;
            var rule = await repository.GetRuleAsync(cancellationToken);
            var isNew = rule is null;
            rule ??= new AlertRule { Name = request.Name.Trim(), CreatedAt = now };
            rule.Name = request.Name.Trim();
            rule.Enabled = request.Enabled;
            rule.MinDownloadMbps = request.MinDownloadMbps;
            rule.MinUploadMbps = request.MinUploadMbps;
            rule.MaxLatencyMs = request.MaxLatencyMs;
            rule.MaxJitterMs = request.MaxJitterMs;
            rule.MaxPacketLossPercent = request.MaxPacketLossPercent;
            rule.DownloadBaselineDropPercent = request.DownloadBaselineDropPercent;
            rule.UploadBaselineDropPercent = request.UploadBaselineDropPercent;
            rule.ConsecutiveFailuresRequired = request.ConsecutiveFailuresRequired;
            rule.ConsecutiveRecoveriesRequired = request.ConsecutiveRecoveriesRequired;
            rule.UpdatedAt = now;

            if (isNew)
            {
                repository.AddRule(rule);
            }

            await repository.SaveChangesAsync(cancellationToken);
            await alertEvaluationService.SynchronizeRuleStateAsync(rule, cancellationToken);
            return Results.Ok(AlertRuleResponse.From(rule));
        });

        endpoints.MapGet("/api/alerts/events", async (
            IAlertStateRepository repository,
            CancellationToken cancellationToken,
            int count = 20) =>
        {
            if (count is < 1 or > 100)
            {
                return Results.BadRequest(new ApiError("invalid_request", "count must be between 1 and 100."));
            }

            var events = await repository.GetRecentEventsAsync(count, cancellationToken);
            return Results.Ok(events.Select(DegradationEventResponse.From).ToArray());
        });

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

    private static bool IsValid(AlertRuleUpdateRequest request) =>
        !string.IsNullOrWhiteSpace(request.Name)
        && request.ConsecutiveFailuresRequired >= 1
        && request.ConsecutiveRecoveriesRequired >= 1
        && NonNegative(request.MinDownloadMbps)
        && NonNegative(request.MinUploadMbps)
        && NonNegative(request.MaxLatencyMs)
        && NonNegative(request.MaxJitterMs)
        && Percentage(request.MaxPacketLossPercent)
        && Percentage(request.DownloadBaselineDropPercent)
        && Percentage(request.UploadBaselineDropPercent);

    private static bool NonNegative(double? value) => !value.HasValue || value.Value >= 0;

    private static bool Percentage(double? value) => !value.HasValue || value.Value is >= 0 and <= 100;
}
