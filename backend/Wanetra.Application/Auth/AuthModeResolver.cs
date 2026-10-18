using Microsoft.Extensions.Configuration;

namespace Wanetra.Application.Auth;

public enum AuthMode
{
    Enabled,
    Disabled,
}

public static class AuthModeResolver
{
    public const string EnvironmentVariable = "WANETRA_AUTH";

    public static AuthMode? FromConfiguration(IConfiguration configuration)
    {
        var value = configuration[EnvironmentVariable]?.Trim();
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        if (string.Equals(value, "disabled", StringComparison.OrdinalIgnoreCase))
        {
            return AuthMode.Disabled;
        }

        if (string.Equals(value, "enabled", StringComparison.OrdinalIgnoreCase))
        {
            return AuthMode.Enabled;
        }

        throw new InvalidOperationException(
            $"{EnvironmentVariable} must be 'enabled' or 'disabled' (or empty), but was '{value}'.");
    }
}
