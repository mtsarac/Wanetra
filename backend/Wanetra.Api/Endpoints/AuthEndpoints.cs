using Wanetra.Api.Auth;
using Wanetra.Api.Contracts;
using Wanetra.Application.Auth;

namespace Wanetra.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/auth");

        group.MapGet("/status", async (HttpContext context, AuthService auth, CancellationToken cancellationToken) =>
            Results.Ok(AuthStatusResponse.From(await auth.GetStatusAsync(SessionCookie.Read(context), cancellationToken))));

        group.MapPost("/setup", (SetupRequest? request, HttpContext context, AuthService auth, CancellationToken cancellationToken) =>
            Run(async () =>
            {
                var grant = await auth.SetupAsync(
                    request?.SetupCode, request?.Username, request?.Password, ThrottleKey(context), cancellationToken);
                SessionCookie.Write(context, grant);
                return Results.Ok(AuthStatusResponse.From(await auth.GetStatusAsync(grant.Token, cancellationToken)));
            }));

        group.MapPost("/login", (LoginRequest? request, HttpContext context, AuthService auth, CancellationToken cancellationToken) =>
            Run(async () =>
            {
                var grant = await auth.LoginAsync(request?.Username, request?.Password, ThrottleKey(context), cancellationToken);
                SessionCookie.Write(context, grant);
                return Results.Ok(AuthStatusResponse.From(await auth.GetStatusAsync(grant.Token, cancellationToken)));
            }));

        group.MapPost("/logout", async (HttpContext context, AuthService auth, CancellationToken cancellationToken) =>
        {
            await auth.LogoutAsync(SessionCookie.Read(context), cancellationToken);
            SessionCookie.Clear(context);
            return Results.NoContent();
        });

        group.MapPost("/change-password", (ChangePasswordRequest? request, HttpContext context, AuthService auth, CancellationToken cancellationToken) =>
            Run(async () =>
            {
                await auth.ChangePasswordAsync(
                    SessionCookie.Read(context), request?.CurrentPassword, request?.NewPassword, ThrottleKey(context), cancellationToken);
                return Results.NoContent();
            }));

        group.MapPost("/disable", (DisableAuthRequest? request, HttpContext context, AuthService auth, CancellationToken cancellationToken) =>
            Run(async () =>
            {
                await auth.DisableAsync(request?.Password, ThrottleKey(context), cancellationToken);
                SessionCookie.Clear(context);
                return Results.NoContent();
            }));

        group.MapPost("/enable", (AuthService auth, CancellationToken cancellationToken) =>
            Run(async () =>
            {
                await auth.EnableAsync(cancellationToken);
                return Results.NoContent();
            }));

        group.MapPost("/metrics-token", (AuthService auth, CancellationToken cancellationToken) =>
            Run(async () => Results.Ok(new MetricsTokenResponse(await auth.RotateMetricsTokenAsync(cancellationToken)))));

        group.MapDelete("/metrics-token", (AuthService auth, CancellationToken cancellationToken) =>
            Run(async () =>
            {
                await auth.RevokeMetricsTokenAsync(cancellationToken);
                return Results.NoContent();
            }));

        return endpoints;
    }

    private static string ThrottleKey(HttpContext context) =>
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    private static async Task<IResult> Run(Func<Task<IResult>> action)
    {
        try
        {
            return await action();
        }
        catch (AuthException exception)
        {
            return ToResult(exception);
        }
    }

    private static IResult ToResult(AuthException exception) => exception.Kind switch
    {
        AuthErrorKind.InvalidCredentials => Results.Json(
            new ApiError("invalid_credentials", exception.Message), statusCode: StatusCodes.Status401Unauthorized),
        AuthErrorKind.Throttled => new ThrottledResult(exception),
        AuthErrorKind.SetupNotAvailable => Results.Conflict(new ApiError("setup_not_available", exception.Message)),
        AuthErrorKind.InvalidSetupCode => Results.Json(
            new ApiError("invalid_setup_code", exception.Message), statusCode: StatusCodes.Status403Forbidden),
        AuthErrorKind.Validation => Results.BadRequest(new ApiError("invalid_request", exception.Message)),
        AuthErrorKind.EnvLocked => Results.Conflict(new ApiError("auth_locked", exception.Message)),
        AuthErrorKind.AlreadyInState => Results.Conflict(new ApiError("auth_unchanged", exception.Message)),
        AuthErrorKind.NoAccount => Results.Conflict(new ApiError("no_account", exception.Message)),
        _ => Results.Problem(),
    };

    private sealed class ThrottledResult(AuthException exception) : IResult
    {
        public async Task ExecuteAsync(HttpContext httpContext)
        {
            var seconds = (int)Math.Ceiling((exception.RetryAfter ?? TimeSpan.FromMinutes(1)).TotalSeconds);
            httpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            httpContext.Response.Headers.RetryAfter = seconds.ToString(System.Globalization.CultureInfo.InvariantCulture);
            await httpContext.Response.WriteAsJsonAsync(new ApiError("too_many_attempts", exception.Message));
        }
    }
}
