namespace Wanetra.Application.Auth;

public sealed record AuthPolicy(bool Enabled, bool EnvLocked, bool AccountExists)
{
    public bool SetupRequired => Enabled && !AccountExists;
}

public sealed record AuthSessionInfo(string Username);

public sealed record AuthStatus(
    bool Enabled,
    bool EnvLocked,
    bool SetupRequired,
    bool AccountExists,
    bool Authenticated,
    string? Username,
    bool MetricsTokenConfigured);

public sealed record AuthSessionGrant(string Token, DateTime ExpiresAt, string Username);

public enum AuthErrorKind
{
    InvalidCredentials,
    Throttled,
    SetupNotAvailable,
    InvalidSetupCode,
    Validation,
    EnvLocked,
    AlreadyInState,
    NoAccount,
}

public sealed class AuthException(AuthErrorKind kind, string message, TimeSpan? retryAfter = null) : Exception(message)
{
    public AuthErrorKind Kind { get; } = kind;
    public TimeSpan? RetryAfter { get; } = retryAfter;
}
