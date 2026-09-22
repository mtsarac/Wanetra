using Wanetra.Api.Contracts;
using Wanetra.Application.Notifications;

namespace Wanetra.Api.Endpoints;

public static class NotificationEndpoints
{
    public static IEndpointRouteBuilder MapNotificationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var notifications = endpoints.MapGroup("/api/notifications");

        notifications.MapGet("", async (
            NotificationConfigurationService service,
            CancellationToken cancellationToken) =>
        {
            var configurations = await service.ListAsync(cancellationToken);
            return Results.Ok(NotificationConfigurationListResponse.From(configurations));
        });

        notifications.MapPut("", async (
            NotificationConfigurationUpdateListRequest request,
            NotificationConfigurationService service,
            CancellationToken cancellationToken) =>
        {
            IReadOnlyList<Domain.NotificationConfiguration> saved;
            try
            {
                saved = await service.SaveAsync(
                    request.Configurations
                        .Select(configuration => (configuration.Provider, configuration.Enabled, configuration.ConfigurationJson))
                        .ToList(),
                    cancellationToken);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new ApiError("invalid_request", ex.Message));
            }

            return Results.Ok(NotificationConfigurationListResponse.From(saved));
        });

        notifications.MapPost("/test", async (
            NotificationTestRequest request,
            NotificationConfigurationService service,
            NotificationDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            string provider;
            string configurationJson;
            if (request.Id.HasValue)
            {
                var stored = (await service.ListAsync(cancellationToken))
                    .FirstOrDefault(configuration => configuration.Id == request.Id.Value);
                if (stored is null)
                {
                    return Results.NotFound();
                }

                provider = stored.Provider;
                configurationJson = stored.ConfigurationJson;
            }
            else if (request.Provider is not null && request.ConfigurationJson is not null)
            {
                provider = request.Provider;
                configurationJson = request.ConfigurationJson;
            }
            else
            {
                return Results.BadRequest(new ApiError("invalid_request", "Provide id or provider with configurationJson."));
            }

            try
            {
                await dispatcher.SendTestAsync(provider, configurationJson, cancellationToken);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new ApiError("invalid_request", ex.Message));
            }
            catch (Exception)
            {
                return Results.Json(new ApiError("notification_failed", "Test notification failed."), statusCode: 502);
            }

            return Results.Ok();
        });

        return endpoints;
    }
}
