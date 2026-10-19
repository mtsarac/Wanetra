using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Wanetra.Api.Auth;
using Wanetra.Api.Contracts;
using Wanetra.Application.Auth;

namespace Wanetra.Api.Tests.Auth;

public class AuthApiTests
{
    private const string Username = "owner";
    private const string Password = "correct-horse-battery";

    private static HttpClient NewClient(WanetraApiFactory factory, bool csrfHeader = true)
    {
        var client = factory.CreateClient();
        if (csrfHeader)
        {
            client.DefaultRequestHeaders.Add(AuthMiddleware.CsrfHeader, "1");
        }

        return client;
    }

    private static string SetupCode(WanetraApiFactory factory) =>
        factory.Services.GetRequiredService<SetupCodeProvider>().Current
        ?? throw new InvalidOperationException("No setup code was generated.");

    private static async Task<HttpResponseMessage> SetupAsync(WanetraApiFactory factory, HttpClient client) =>
        await client.PostAsJsonAsync("/api/auth/setup", new SetupRequest(SetupCode(factory), Username, Password));

    private static async Task<AuthStatusResponse> StatusAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<AuthStatusResponse>("/api/auth/status"))!;

    [Fact]
    public async Task Fresh_install_requires_setup_and_blocks_the_api_but_not_health()
    {
        await using var factory = new AuthApiFactory();
        using var client = NewClient(factory);

        var status = await StatusAsync(client);
        Assert.True(status.AuthEnabled);
        Assert.True(status.SetupRequired);
        Assert.False(status.Authenticated);
        Assert.False(status.AccountExists);

        var blocked = await client.GetAsync("/api/settings");
        Assert.Equal(HttpStatusCode.Unauthorized, blocked.StatusCode);
        Assert.Equal("setup_required", (await blocked.Content.ReadFromJsonAsync<ApiError>())!.Code);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/ready")).StatusCode);
        Assert.NotEqual(HttpStatusCode.Unauthorized, (await client.GetAsync("/")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/openapi/v1.json")).StatusCode);
    }

    [Fact]
    public async Task Setup_rejects_a_wrong_code_then_creates_the_account_and_signs_in()
    {
        await using var factory = new AuthApiFactory();
        using var client = NewClient(factory);

        var wrong = await client.PostAsJsonAsync("/api/auth/setup", new SetupRequest("AAAA-AAAA-AAAA", Username, Password));
        Assert.Equal(HttpStatusCode.Forbidden, wrong.StatusCode);

        var created = await SetupAsync(factory, client);
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        Assert.Contains(SessionCookie.Name, string.Join(';', created.Headers.GetValues("Set-Cookie")));
        Assert.Contains("httponly", string.Join(';', created.Headers.GetValues("Set-Cookie")), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", string.Join(';', created.Headers.GetValues("Set-Cookie")), StringComparison.OrdinalIgnoreCase);

        var status = await StatusAsync(client);
        Assert.True(status.Authenticated);
        Assert.False(status.SetupRequired);
        Assert.Equal(Username, status.Username);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/settings")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/openapi/v1.json")).StatusCode);

        var again = await client.PostAsJsonAsync("/api/auth/setup", new SetupRequest("AAAA-AAAA-AAAA", "other", Password));
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    [Fact]
    public async Task Setup_validates_username_and_password_without_consuming_the_code()
    {
        await using var factory = new AuthApiFactory();
        using var client = NewClient(factory);
        var code = SetupCode(factory);

        var shortPassword = await client.PostAsJsonAsync("/api/auth/setup", new SetupRequest(code, Username, "short"));
        var badUsername = await client.PostAsJsonAsync("/api/auth/setup", new SetupRequest(code, "a b", Password));

        Assert.Equal(HttpStatusCode.BadRequest, shortPassword.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, badUsername.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/setup", new SetupRequest(code, Username, Password))).StatusCode);
    }

    [Fact]
    public async Task Login_logout_and_unauthenticated_access_after_setup()
    {
        await using var factory = new AuthApiFactory();
        using var owner = NewClient(factory);
        await SetupAsync(factory, owner);

        using var stranger = NewClient(factory);
        var denied = await stranger.GetAsync("/api/settings");
        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        Assert.Equal("unauthorized", (await denied.Content.ReadFromJsonAsync<ApiError>())!.Code);

        var bad = await stranger.PostAsJsonAsync("/api/auth/login", new LoginRequest(Username, "not-the-password"));
        Assert.Equal(HttpStatusCode.Unauthorized, bad.StatusCode);
        var unknownUser = await stranger.PostAsJsonAsync("/api/auth/login", new LoginRequest("nobody", Password));
        Assert.Equal(HttpStatusCode.Unauthorized, unknownUser.StatusCode);

        var ok = await stranger.PostAsJsonAsync("/api/auth/login", new LoginRequest(Username.ToUpperInvariant(), Password));
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await stranger.GetAsync("/api/settings")).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await stranger.PostAsync("/api/auth/logout", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await stranger.GetAsync("/api/settings")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync("/api/settings")).StatusCode);
    }

    [Fact]
    public async Task Repeated_failed_logins_are_throttled()
    {
        await using var factory = new AuthApiFactory();
        using var client = NewClient(factory);
        await SetupAsync(factory, client);
        using var stranger = NewClient(factory);

        for (var i = 0; i < LoginThrottle.PerKeyLimit; i++)
        {
            var failed = await stranger.PostAsJsonAsync("/api/auth/login", new LoginRequest(Username, "wrong-password"));
            Assert.Equal(HttpStatusCode.Unauthorized, failed.StatusCode);
        }

        var throttled = await stranger.PostAsJsonAsync("/api/auth/login", new LoginRequest(Username, Password));
        Assert.Equal(HttpStatusCode.TooManyRequests, throttled.StatusCode);
        Assert.True(throttled.Headers.RetryAfter?.Delta > TimeSpan.Zero);
    }

    [Fact]
    public async Task State_changing_requests_need_the_csrf_header()
    {
        await using var factory = new AuthApiFactory();
        using var withHeader = NewClient(factory);
        await SetupAsync(factory, withHeader);

        using var withoutHeader = NewClient(factory, csrfHeader: false);
        await withoutHeader.PostAsJsonAsync("/api/auth/login", new LoginRequest(Username, Password));

        var rejected = await withoutHeader.PostAsync("/api/speedtests/run", null);
        Assert.Equal(HttpStatusCode.Forbidden, rejected.StatusCode);
        Assert.Equal("csrf_header_required", (await rejected.Content.ReadFromJsonAsync<ApiError>())!.Code);
        Assert.Equal(HttpStatusCode.OK, (await withoutHeader.GetAsync("/api/settings")).StatusCode);
    }

    [Fact]
    public async Task Metrics_accepts_a_session_or_the_generated_bearer_token_only()
    {
        await using var factory = new AuthApiFactory();
        using var owner = NewClient(factory);
        await SetupAsync(factory, owner);

        using var anonymous = NewClient(factory);
        var denied = await anonymous.GetAsync("/metrics");
        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        Assert.Equal("Bearer", denied.Headers.WwwAuthenticate.Single().Scheme);
        Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync("/metrics")).StatusCode);

        var tokenResponse = await owner.PostAsync("/api/auth/metrics-token", null);
        var token = (await tokenResponse.Content.ReadFromJsonAsync<MetricsTokenResponse>())!.Token;
        Assert.StartsWith("wnm_", token);
        Assert.True((await StatusAsync(owner)).MetricsTokenConfigured);

        using var scraper = NewClient(factory);
        scraper.DefaultRequestHeaders.Authorization = new("Bearer", token);
        Assert.Equal(HttpStatusCode.OK, (await scraper.GetAsync("/metrics")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await scraper.GetAsync("/api/settings")).StatusCode);

        using var wrong = NewClient(factory);
        wrong.DefaultRequestHeaders.Authorization = new("Bearer", "wnm_wrong");
        Assert.Equal(HttpStatusCode.Unauthorized, (await wrong.GetAsync("/metrics")).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await owner.DeleteAsync("/api/auth/metrics-token")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await scraper.GetAsync("/metrics")).StatusCode);
    }

    [Fact]
    public async Task Change_password_checks_the_current_one_and_revokes_other_sessions()
    {
        await using var factory = new AuthApiFactory();
        using var first = NewClient(factory);
        await SetupAsync(factory, first);
        using var second = NewClient(factory);
        await second.PostAsJsonAsync("/api/auth/login", new LoginRequest(Username, Password));

        var wrongCurrent = await first.PostAsJsonAsync("/api/auth/change-password", new ChangePasswordRequest("nope-nope-nope", "brand-new-password"));
        Assert.Equal(HttpStatusCode.Unauthorized, wrongCurrent.StatusCode);
        var tooShort = await first.PostAsJsonAsync("/api/auth/change-password", new ChangePasswordRequest(Password, "short"));
        Assert.Equal(HttpStatusCode.BadRequest, tooShort.StatusCode);

        var changed = await first.PostAsJsonAsync("/api/auth/change-password", new ChangePasswordRequest(Password, "brand-new-password"));
        Assert.Equal(HttpStatusCode.NoContent, changed.StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await first.GetAsync("/api/settings")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await second.GetAsync("/api/settings")).StatusCode);

        using var fresh = NewClient(factory);
        Assert.Equal(HttpStatusCode.Unauthorized, (await fresh.PostAsJsonAsync("/api/auth/login", new LoginRequest(Username, Password))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await fresh.PostAsJsonAsync("/api/auth/login", new LoginRequest(Username, "brand-new-password"))).StatusCode);
    }

    [Fact]
    public async Task Authentication_can_be_disabled_with_the_password_and_enabled_again()
    {
        await using var factory = new AuthApiFactory();
        using var owner = NewClient(factory);
        await SetupAsync(factory, owner);

        var wrong = await owner.PostAsJsonAsync("/api/auth/disable", new DisableAuthRequest("nope-nope-nope"));
        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        Assert.True((await StatusAsync(owner)).AuthEnabled);

        Assert.Equal(HttpStatusCode.NoContent, (await owner.PostAsJsonAsync("/api/auth/disable", new DisableAuthRequest(Password))).StatusCode);

        using var anonymous = NewClient(factory);
        var openStatus = await StatusAsync(anonymous);
        Assert.False(openStatus.AuthEnabled);
        Assert.True(openStatus.Authenticated);
        Assert.True(openStatus.AccountExists);
        Assert.Equal(HttpStatusCode.OK, (await anonymous.GetAsync("/api/settings")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await anonymous.GetAsync("/metrics")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await anonymous.PostAsJsonAsync("/api/auth/disable", new DisableAuthRequest(Password))).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await anonymous.PostAsync("/api/auth/enable", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/settings")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await anonymous.PostAsJsonAsync("/api/auth/login", new LoginRequest(Username, Password))).StatusCode);
    }

    [Fact]
    public async Task Environment_variable_overrides_the_stored_choice_and_locks_the_ui()
    {
        await using var factory = new AuthApiFactory("disabled");
        using var client = NewClient(factory);

        var status = await StatusAsync(client);
        Assert.False(status.AuthEnabled);
        Assert.True(status.EnvLocked);
        Assert.False(status.SetupRequired);
        Assert.Null(factory.Services.GetRequiredService<SetupCodeProvider>().Current);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/settings")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync("/api/auth/enable", null)).StatusCode);
    }

    [Fact]
    public async Task Environment_enabled_blocks_disabling_from_the_ui()
    {
        await using var factory = new AuthApiFactory("enabled");
        using var client = NewClient(factory);
        await SetupAsync(factory, client);

        var response = await client.PostAsJsonAsync("/api/auth/disable", new DisableAuthRequest(Password));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("auth_locked", (await response.Content.ReadFromJsonAsync<ApiError>())!.Code);
    }

    [Fact]
    public async Task An_invalid_environment_value_stops_startup()
    {
        await using var factory = new AuthApiFactory("sometimes");

        Assert.ThrowsAny<Exception>(() => factory.CreateClient());
        await Task.CompletedTask;
    }
}
