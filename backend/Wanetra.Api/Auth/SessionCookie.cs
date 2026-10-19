using Wanetra.Application.Auth;

namespace Wanetra.Api.Auth;

public static class SessionCookie
{
    public const string Name = "wanetra_session";

    public static string? Read(HttpContext context) =>
        context.Request.Cookies.TryGetValue(Name, out var value) ? value : null;

    public static void Write(HttpContext context, AuthSessionGrant grant) =>
        context.Response.Cookies.Append(Name, grant.Token, Options(context, grant.ExpiresAt));

    public static void Clear(HttpContext context) =>
        context.Response.Cookies.Delete(Name, Options(context, null));

    private static CookieOptions Options(HttpContext context, DateTime? expiresAt) => new()
    {
        HttpOnly = true,
        SameSite = SameSiteMode.Strict,
        Secure = context.Request.IsHttps,
        Path = "/",
        Expires = expiresAt is null ? null : new DateTimeOffset(DateTime.SpecifyKind(expiresAt.Value, DateTimeKind.Utc)),
    };
}
