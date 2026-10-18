using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Wanetra.Domain;

namespace Wanetra.Application.Auth;

public sealed partial class AuthService(
    IAuthRepository repository,
    IConfiguration configuration,
    SetupCodeProvider setupCodes,
    LoginThrottle throttle,
    TimeProvider timeProvider,
    ILogger<AuthService> logger)
{
    public const int MinPasswordLength = 8;
    public const int MaxPasswordLength = 128;
    public static readonly TimeSpan SessionLifetime = TimeSpan.FromDays(30);
    private const string MetricsTokenPrefix = "wnm_";
    private static readonly SemaphoreSlim SetupGate = new(1, 1);
    private static readonly string DummyHash = PasswordHasher.Hash("wanetra-dummy-password");

    [GeneratedRegex("^[A-Za-z0-9._-]{3,64}$")]
    private static partial Regex UsernamePattern();

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        var policy = await GetPolicyAsync(cancellationToken);
        if (!policy.SetupRequired)
        {
            setupCodes.Clear();
            return;
        }

        var code = setupCodes.Generate();
        logger.LogWarning(
            "Wanetra has no account yet. Open the web UI and enter the setup code {SetupCode} to create it. "
            + "Set {EnvironmentVariable}=disabled to run without authentication.",
            code,
            AuthModeResolver.EnvironmentVariable);
    }

    public async Task<AuthPolicy> GetPolicyAsync(CancellationToken cancellationToken)
    {
        var account = await repository.GetAccountAsync(cancellationToken);
        var envMode = AuthModeResolver.FromConfiguration(configuration);
        var enabled = envMode switch
        {
            AuthMode.Disabled => false,
            AuthMode.Enabled => true,
            _ => account?.AuthEnabled ?? true,
        };

        return new AuthPolicy(enabled, envMode is not null, account is not null);
    }

    public async Task<AuthStatus> GetStatusAsync(string? sessionToken, CancellationToken cancellationToken)
    {
        var policy = await GetPolicyAsync(cancellationToken);
        var session = await ValidateSessionAsync(sessionToken, cancellationToken);
        var authenticated = !policy.Enabled || session is not null;
        var account = authenticated ? await repository.GetAccountAsync(cancellationToken) : null;

        return new AuthStatus(
            policy.Enabled,
            policy.EnvLocked,
            policy.SetupRequired,
            policy.AccountExists,
            authenticated,
            session?.Username,
            account?.MetricsTokenHash is not null);
    }

    public async Task<AuthSessionGrant> SetupAsync(
        string? setupCode,
        string? username,
        string? password,
        string throttleKey,
        CancellationToken cancellationToken)
    {
        EnsureNotThrottled(throttleKey);

        await SetupGate.WaitAsync(cancellationToken);
        try
        {
            var policy = await GetPolicyAsync(cancellationToken);
            if (!policy.SetupRequired)
            {
                throw new AuthException(AuthErrorKind.SetupNotAvailable, "Setup is not available.");
            }

            if (!setupCodes.Verify(setupCode))
            {
                throttle.RecordFailure(throttleKey);
                throw new AuthException(AuthErrorKind.InvalidSetupCode, "The setup code is not valid.");
            }

            ValidateUsername(username);
            ValidatePassword(password);

            var now = timeProvider.GetUtcNow().UtcDateTime;
            repository.AddAccount(new AuthAccount
            {
                Username = username!.Trim(),
                PasswordHash = PasswordHasher.Hash(password!),
                AuthEnabled = true,
                CreatedAt = now,
                UpdatedAt = now,
            });
            var grant = AddSession(username.Trim(), now);
            await repository.SaveChangesAsync(cancellationToken);

            setupCodes.Clear();
            throttle.Reset(throttleKey);
            logger.LogInformation("Account created during first-run setup.");
            return grant;
        }
        finally
        {
            SetupGate.Release();
        }
    }

    public async Task<AuthSessionGrant> LoginAsync(
        string? username,
        string? password,
        string throttleKey,
        CancellationToken cancellationToken)
    {
        EnsureNotThrottled(throttleKey);

        var account = await repository.GetAccountAsync(cancellationToken);
        var passwordMatches = PasswordHasher.Verify(password ?? string.Empty, account?.PasswordHash ?? DummyHash);
        var usernameMatches = account is not null
            && string.Equals(account.Username, username?.Trim(), StringComparison.OrdinalIgnoreCase);
        if (account is null || !passwordMatches || !usernameMatches)
        {
            throttle.RecordFailure(throttleKey);
            throw new AuthException(AuthErrorKind.InvalidCredentials, "Invalid username or password.");
        }

        throttle.Reset(throttleKey);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        await repository.DeleteExpiredSessionsAsync(now, cancellationToken);
        var grant = AddSession(account.Username, now);
        await repository.SaveChangesAsync(cancellationToken);
        return grant;
    }

    public async Task LogoutAsync(string? sessionToken, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(sessionToken))
        {
            await repository.DeleteSessionAsync(HashToken(sessionToken), cancellationToken);
        }
    }

    public async Task<AuthSessionInfo?> ValidateSessionAsync(string? sessionToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(sessionToken))
        {
            return null;
        }

        var session = await repository.FindSessionAsync(HashToken(sessionToken), cancellationToken);
        if (session is null || session.ExpiresAt <= timeProvider.GetUtcNow().UtcDateTime)
        {
            return null;
        }

        var account = await repository.GetAccountAsync(cancellationToken);
        return account is null ? null : new AuthSessionInfo(account.Username);
    }

    public async Task ChangePasswordAsync(
        string? sessionToken,
        string? currentPassword,
        string? newPassword,
        string throttleKey,
        CancellationToken cancellationToken)
    {
        var account = await RequireAccountAsync(cancellationToken);
        await VerifyPasswordAsync(account, currentPassword, throttleKey);
        ValidatePassword(newPassword);

        account.PasswordHash = PasswordHasher.Hash(newPassword!);
        account.UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;
        await repository.SaveChangesAsync(cancellationToken);
        await repository.DeleteSessionsExceptAsync(
            string.IsNullOrEmpty(sessionToken) ? null : HashToken(sessionToken),
            cancellationToken);
    }

    public async Task DisableAsync(string? password, string throttleKey, CancellationToken cancellationToken)
    {
        var policy = await GetPolicyAsync(cancellationToken);
        if (policy.EnvLocked)
        {
            throw new AuthException(
                AuthErrorKind.EnvLocked,
                $"Authentication is controlled by the {AuthModeResolver.EnvironmentVariable} environment variable.");
        }

        var account = await RequireAccountAsync(cancellationToken);
        if (!account.AuthEnabled)
        {
            throw new AuthException(AuthErrorKind.AlreadyInState, "Authentication is already disabled.");
        }

        await VerifyPasswordAsync(account, password, throttleKey);
        account.AuthEnabled = false;
        account.UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;
        await repository.SaveChangesAsync(cancellationToken);
        await repository.DeleteSessionsExceptAsync(null, cancellationToken);
        logger.LogWarning("Authentication was disabled from the web UI.");
    }

    public async Task EnableAsync(CancellationToken cancellationToken)
    {
        var policy = await GetPolicyAsync(cancellationToken);
        if (policy.EnvLocked)
        {
            throw new AuthException(
                AuthErrorKind.EnvLocked,
                $"Authentication is controlled by the {AuthModeResolver.EnvironmentVariable} environment variable.");
        }

        var account = await RequireAccountAsync(cancellationToken);
        if (account.AuthEnabled)
        {
            throw new AuthException(AuthErrorKind.AlreadyInState, "Authentication is already enabled.");
        }

        account.AuthEnabled = true;
        account.UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;
        await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task<string> RotateMetricsTokenAsync(CancellationToken cancellationToken)
    {
        var account = await RequireAccountAsync(cancellationToken);
        var token = MetricsTokenPrefix + Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        var now = timeProvider.GetUtcNow().UtcDateTime;
        account.MetricsTokenHash = HashToken(token);
        account.MetricsTokenCreatedAt = now;
        account.UpdatedAt = now;
        await repository.SaveChangesAsync(cancellationToken);
        return token;
    }

    public async Task RevokeMetricsTokenAsync(CancellationToken cancellationToken)
    {
        var account = await RequireAccountAsync(cancellationToken);
        account.MetricsTokenHash = null;
        account.MetricsTokenCreatedAt = null;
        account.UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;
        await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> ValidateMetricsTokenAsync(string? token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(token))
        {
            return false;
        }

        var account = await repository.GetAccountAsync(cancellationToken);
        if (account?.MetricsTokenHash is null)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(HashToken(token)),
            Encoding.ASCII.GetBytes(account.MetricsTokenHash));
    }

    private AuthSessionGrant AddSession(string username, DateTime now)
    {
        var token = Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        var expiresAt = now + SessionLifetime;
        repository.AddSession(new AuthSession
        {
            TokenHash = HashToken(token),
            CreatedAt = now,
            ExpiresAt = expiresAt,
        });
        return new AuthSessionGrant(token, expiresAt, username);
    }

    private async Task<AuthAccount> RequireAccountAsync(CancellationToken cancellationToken) =>
        await repository.GetAccountAsync(cancellationToken)
        ?? throw new AuthException(AuthErrorKind.NoAccount, "No account exists yet.");

    private async Task VerifyPasswordAsync(AuthAccount account, string? password, string throttleKey)
    {
        EnsureNotThrottled(throttleKey);
        if (!PasswordHasher.Verify(password ?? string.Empty, account.PasswordHash))
        {
            throttle.RecordFailure(throttleKey);
            throw new AuthException(AuthErrorKind.InvalidCredentials, "The password is not correct.");
        }

        throttle.Reset(throttleKey);
        await Task.CompletedTask;
    }

    private void EnsureNotThrottled(string throttleKey)
    {
        if (throttle.IsBlocked(throttleKey, out var retryAfter))
        {
            throw new AuthException(
                AuthErrorKind.Throttled,
                "Too many failed attempts. Try again later.",
                retryAfter);
        }
    }

    private static void ValidateUsername(string? username)
    {
        if (username is null || !UsernamePattern().IsMatch(username.Trim()))
        {
            throw new AuthException(
                AuthErrorKind.Validation,
                "Username must be 3 to 64 characters: letters, digits, dot, underscore or hyphen.");
        }
    }

    private static void ValidatePassword(string? password)
    {
        if (password is null || password.Length < MinPasswordLength || password.Length > MaxPasswordLength)
        {
            throw new AuthException(
                AuthErrorKind.Validation,
                $"Password must be {MinPasswordLength} to {MaxPasswordLength} characters long.");
        }
    }

    private static string HashToken(string token) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
