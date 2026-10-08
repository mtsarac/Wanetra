using Wanetra.Api.Contracts;
using Wanetra.Application.Settings;

namespace Wanetra.Api.Endpoints;

public static class SettingsEndpoints
{
    // Single /api/settings group: future authorization seam
    public static IEndpointRouteBuilder MapSettingsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/settings");

        group.MapGet("", (SettingsService service) =>
        {
            var settings = service.GetAll();
            return Results.Ok(ToResponse(settings));
        });

        group.MapPut("", async (
            UpdateSettingsRequest? request,
            SettingsService service,
            CancellationToken cancellationToken) =>
        {
            if (request?.Values is null)
            {
                return Results.BadRequest(new ApiError("invalid_request", "Values dictionary is required."));
            }

            try
            {
                var updated = await service.UpdateAsync(request.Values, cancellationToken);
                return Results.Ok(ToResponse(updated));
            }
            catch (SettingsException ex)
            {
                return ex.Kind switch
                {
                    SettingsErrorKind.UnknownSetting => Results.BadRequest(new ApiError("unknown_setting", ex.Message)),
                    SettingsErrorKind.SettingLocked => Results.Conflict(new ApiError("setting_locked", ex.Message)),
                    _ => Results.BadRequest(new ApiError("invalid_setting", ex.Message)),
                };
            }
        });

        return endpoints;
    }

    private static SettingsListResponse ToResponse(IReadOnlyList<EffectiveSetting> settings) =>
        new(settings.Select(s => new SettingResponse(
            Key: s.Key,
            Group: s.Group,
            Type: s.Type,
            Value: s.Value,
            DefaultValue: s.DefaultValue,
            Nullable: s.Nullable,
            Source: s.SourceString,
            Locked: s.Locked,
            LockReason: s.LockReason,
            EnvironmentVariable: s.EnvironmentVariable,
            Options: s.Options,
            Min: s.Min,
            Max: s.Max,
            UpdatedAt: s.UpdatedAt?.ToString("yyyy-MM-dd'T'HH:mm:ss.fffK")
        )).ToList());
}
