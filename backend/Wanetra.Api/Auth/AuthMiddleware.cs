using Wanetra.Api.Contracts;
using Wanetra.Application.Auth;

namespace Wanetra.Api.Auth;

public sealed class AuthMiddleware(RequestDelegate next)
{
    // Browsers cannot add a custom header cross-origin without a CORS preflight, which Wanetra never answers.
    public const string CsrfHeader = "X-Wanetra-Request";

    private static readonly string[] PublicPaths =
    [
        "/api/auth/status",
        "/api/auth/setup",
        "/api/auth/login",
        "/api/auth/logout",
    ];

    public async Task InvokeAsync(HttpContext context, AuthService auth)
    {
        var path = context.Request.Path;
        var isMetrics = path.StartsWithSegments("/metrics");
        var isProtected = path.StartsWithSegments("/api") || path.StartsWithSegments("/openapi") || isMetrics;
        if (!isProtected)
        {
            await next(context);
            return;
        }

        var cancellationToken = context.RequestAborted;
        var policy = await auth.GetPolicyAsync(cancellationToken);
        var isPublic = PublicPaths.Any(publicPath => path.Equals(publicPath, StringComparison.OrdinalIgnoreCase));
        if (!policy.Enabled || isPublic)
        {
            await next(context);
            return;
        }

        if (isMetrics && await HasValidBearerAsync(context, auth, cancellationToken))
        {
            await next(context);
            return;
        }

        var session = await auth.ValidateSessionAsync(SessionCookie.Read(context), cancellationToken);
        if (session is null)
        {
            await Reject(context, policy, isMetrics);
            return;
        }

        if (!IsSafeMethod(context.Request.Method) && !context.Request.Headers.ContainsKey(CsrfHeader))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(
                new ApiError("csrf_header_required", $"State-changing requests must send the {CsrfHeader} header."),
                cancellationToken);
            return;
        }

        await next(context);
    }

    private static async Task Reject(HttpContext context, AuthPolicy policy, bool isMetrics)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        if (isMetrics)
        {
            context.Response.Headers.WWWAuthenticate = "Bearer";
        }

        await context.Response.WriteAsJsonAsync(
            policy.SetupRequired
                ? new ApiError("setup_required", "Create the first account to use Wanetra.")
                : new ApiError("unauthorized", "Authentication is required."),
            context.RequestAborted);
    }

    private static async Task<bool> HasValidBearerAsync(HttpContext context, AuthService auth, CancellationToken cancellationToken)
    {
        var header = context.Request.Headers.Authorization.ToString();
        const string prefix = "Bearer ";
        return header.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            && await auth.ValidateMetricsTokenAsync(header[prefix.Length..].Trim(), cancellationToken);
    }

    private static bool IsSafeMethod(string method) =>
        HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method);
}
