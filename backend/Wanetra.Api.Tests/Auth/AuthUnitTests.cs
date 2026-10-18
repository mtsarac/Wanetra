using Microsoft.Extensions.Configuration;
using Wanetra.Application.Auth;

namespace Wanetra.Api.Tests.Auth;

public class AuthUnitTests
{
    [Fact]
    public void Password_hash_round_trips_and_rejects_other_passwords()
    {
        var hash = PasswordHasher.Hash("correct horse", iterations: 1_000);

        Assert.True(PasswordHasher.Verify("correct horse", hash));
        Assert.False(PasswordHasher.Verify("wrong horse", hash));
        Assert.NotEqual(hash, PasswordHasher.Hash("correct horse", iterations: 1_000));
    }

    [Theory]
    [InlineData("")]
    [InlineData("plain")]
    [InlineData("pbkdf2-sha256$0$AAAA$AAAA")]
    [InlineData("pbkdf2-sha256$abc$AAAA$AAAA")]
    [InlineData("pbkdf2-sha256$1000$not-base64$AAAA")]
    [InlineData("other$1000$AAAA$AAAA")]
    public void Malformed_hashes_never_verify(string stored)
    {
        Assert.False(PasswordHasher.Verify("anything", stored));
    }

    [Fact]
    public void Setup_code_ignores_case_and_separators_and_clears()
    {
        var provider = new SetupCodeProvider();
        var code = provider.Generate();

        Assert.Matches("^[A-Z2-9]{4}-[A-Z2-9]{4}-[A-Z2-9]{4}$", code);
        Assert.True(provider.Verify(code.ToLowerInvariant().Replace("-", " ")));
        Assert.False(provider.Verify("AAAA-AAAA-AAAA"));
        Assert.False(provider.Verify(null));

        provider.Clear();
        Assert.False(provider.Verify(code));
        Assert.Null(provider.Current);
    }

    [Fact]
    public void Throttle_blocks_after_five_failures_and_recovers_after_the_window()
    {
        var time = new ManualTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var throttle = new LoginThrottle(time);

        for (var i = 0; i < LoginThrottle.PerKeyLimit; i++)
        {
            Assert.False(throttle.IsBlocked("a", out _));
            throttle.RecordFailure("a");
        }

        Assert.True(throttle.IsBlocked("a", out var retryAfter));
        Assert.Equal(LoginThrottle.Window, retryAfter);
        Assert.False(throttle.IsBlocked("b", out _));

        time.Advance(LoginThrottle.Window + TimeSpan.FromSeconds(1));
        Assert.False(throttle.IsBlocked("a", out _));
    }

    [Fact]
    public void Throttle_reset_clears_one_key_and_global_limit_applies_to_all_keys()
    {
        var time = new ManualTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var throttle = new LoginThrottle(time);

        throttle.RecordFailure("a");
        throttle.Reset("a");
        Assert.False(throttle.IsBlocked("a", out _));

        for (var i = 0; i < LoginThrottle.GlobalLimit; i++)
        {
            throttle.RecordFailure($"key-{i}");
        }

        Assert.True(throttle.IsBlocked("fresh", out _));
    }

    [Theory]
    [InlineData("enabled", AuthMode.Enabled)]
    [InlineData("DISABLED", AuthMode.Disabled)]
    [InlineData(" disabled ", AuthMode.Disabled)]
    public void Auth_mode_is_parsed_from_configuration(string value, AuthMode expected)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [AuthModeResolver.EnvironmentVariable] = value })
            .Build();

        Assert.Equal(expected, AuthModeResolver.FromConfiguration(configuration));
    }

    [Fact]
    public void Auth_mode_is_unset_when_blank_and_rejects_unknown_values()
    {
        var blank = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [AuthModeResolver.EnvironmentVariable] = "  " })
            .Build();
        var invalid = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [AuthModeResolver.EnvironmentVariable] = "maybe" })
            .Build();

        Assert.Null(AuthModeResolver.FromConfiguration(blank));
        Assert.Throws<InvalidOperationException>(() => { AuthModeResolver.FromConfiguration(invalid); });
    }
}
