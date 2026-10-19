using Wanetra.Application.Auth;

namespace Wanetra.Api.Contracts;

public sealed record AuthStatusResponse(
    bool AuthEnabled,
    bool EnvLocked,
    bool SetupRequired,
    bool AccountExists,
    bool Authenticated,
    string? Username,
    bool MetricsTokenConfigured)
{
    public static AuthStatusResponse From(AuthStatus status) => new(
        status.Enabled,
        status.EnvLocked,
        status.SetupRequired,
        status.AccountExists,
        status.Authenticated,
        status.Username,
        status.MetricsTokenConfigured);
}

public sealed record SetupRequest(string? SetupCode, string? Username, string? Password);

public sealed record LoginRequest(string? Username, string? Password);

public sealed record ChangePasswordRequest(string? CurrentPassword, string? NewPassword);

public sealed record DisableAuthRequest(string? Password);

public sealed record MetricsTokenResponse(string Token);
