using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wanetra.Api.Contracts;

namespace Wanetra.Api.Tests.SpeedTests;

public class NotificationApiTests
{
    private const string NtfyJson = """{"serverUrl":"https://ntfy.sh","topic":"wanetra","username":"wanetra-user","password":"private-password","token":"private-token"}""";
    private const string WebhookJson = """{"url":"https://example.com/hook"}""";

    [Fact]
    public async Task Get_returns_empty_list_initially()
    {
        await using var factory = new WanetraApiFactory();
        var client = factory.CreateClient();

        var list = await client.GetFromJsonAsync<NotificationConfigurationListResponse>("/api/notifications");

        Assert.NotNull(list);
        Assert.Empty(list.Configurations);
    }
    [Fact]
    public async Task Put_ignores_empty_disabled_providers_but_rejects_an_enabled_incomplete_provider()
    {
        await using var factory = new WanetraApiFactory();
        var client = factory.CreateClient();

        var empty = await client.PutAsJsonAsync("/api/notifications", new NotificationConfigurationUpdateListRequest(
        [
            new NotificationConfigurationUpdateRequest("ntfy", false, """{"serverUrl":"","topic":""}"""),
            new NotificationConfigurationUpdateRequest("webhook", false, """{"url":"","method":"POST"}"""),
        ]));
        Assert.Equal(HttpStatusCode.OK, empty.StatusCode);
        Assert.Empty((await empty.Content.ReadFromJsonAsync<NotificationConfigurationListResponse>())!.Configurations);

        var enabled = await client.PutAsJsonAsync("/api/notifications", new NotificationConfigurationUpdateListRequest(
            [new NotificationConfigurationUpdateRequest("ntfy", true, """{"serverUrl":"","topic":""}""")]));
        Assert.Equal(HttpStatusCode.BadRequest, enabled.StatusCode);
    }


    [Fact]
    public async Task Put_persists_and_redacts_configuration()
    {
        await using var factory = new WanetraApiFactory();
        var client = factory.CreateClient();

        var update = await client.PutAsJsonAsync("/api/notifications", new NotificationConfigurationUpdateListRequest(
        [
            new NotificationConfigurationUpdateRequest("ntfy", true, NtfyJson),
            new NotificationConfigurationUpdateRequest("webhook", false, WebhookJson),
        ]));

        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var saved = await update.Content.ReadFromJsonAsync<NotificationConfigurationListResponse>();

        Assert.NotNull(saved);
        Assert.Equal(2, saved.Configurations.Count);
        Assert.True(saved.Configurations[0].HasConfiguration);
        Assert.True(saved.Configurations[0].Enabled);

        var reloaded = await client.GetFromJsonAsync<NotificationConfigurationListResponse>("/api/notifications");
        Assert.NotNull(reloaded);
        Assert.Equal(2, reloaded.Configurations.Count);
        Assert.True(reloaded.Configurations.All(configuration => configuration.HasConfiguration));
        var rawResponse = await client.GetStringAsync("/api/notifications");
        Assert.DoesNotContain("private-password", rawResponse);
        Assert.DoesNotContain("private-token", rawResponse);
    }
    [Fact]
    public async Task Webhook_method_headers_and_id_survive_blank_toggle_save_without_exposing_secrets()
    {
        await using var factory = new WanetraApiFactory();
        var client = factory.CreateClient();
        var initial = await client.PutAsJsonAsync("/api/notifications", new NotificationConfigurationUpdateListRequest(
            [new NotificationConfigurationUpdateRequest(
                "webhook",
                true,
                """{"url":"https://example.com/hook?secret=private","method":"PUT","headers":{"X-Token":"private-header"}}""")]));
        Assert.Equal(HttpStatusCode.OK, initial.StatusCode);
        var savedInitial = await initial.Content.ReadFromJsonAsync<NotificationConfigurationListResponse>();
        Assert.NotNull(savedInitial);
        var originalId = savedInitial.Configurations.Single().Id;

        var toggled = await client.PutAsJsonAsync("/api/notifications", new NotificationConfigurationUpdateListRequest(
            [new NotificationConfigurationUpdateRequest("webhook", false, """{"url":"","method":""}""")]));
        Assert.Equal(HttpStatusCode.OK, toggled.StatusCode);
        var response = await toggled.Content.ReadFromJsonAsync<NotificationConfigurationListResponse>();

        Assert.NotNull(response);
        var webhook = Assert.Single(response.Configurations);
        Assert.Equal(originalId, webhook.Id);
        Assert.Equal("PUT", webhook.Method);
        Assert.True(webhook.HasUrl);
        Assert.True(webhook.HasHeaders);
        var raw = await client.GetStringAsync("/api/notifications");
        Assert.DoesNotContain("private", raw);
        Assert.DoesNotContain("example.com", raw);

        var methodChanged = await client.PutAsJsonAsync("/api/notifications", new NotificationConfigurationUpdateListRequest(
            [new NotificationConfigurationUpdateRequest("webhook", false, """{"url":"","method":"GET"}""")]));
        var changed = await methodChanged.Content.ReadFromJsonAsync<NotificationConfigurationListResponse>();
        Assert.Equal("GET", Assert.Single(changed!.Configurations).Method);
    }
    [Fact]
    public async Task Configuration_metadata_exposes_ntfy_fields_but_not_credentials()
    {
        await using var factory = new WanetraApiFactory();
        var client = factory.CreateClient();
        var saved = await client.PutAsJsonAsync("/api/notifications", new NotificationConfigurationUpdateListRequest(
            [new NotificationConfigurationUpdateRequest(
                "ntfy",
                true,
                """{"serverUrl":"https://ntfy.sh","topic":"wanetra","username":"private-user","password":"private-password","token":"private-token","priority":"high","tags":["warning","wan"]}""")]));
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);

        var response = await client.GetStringAsync("/api/notifications");
        Assert.Contains("https://ntfy.sh", response);
        Assert.Contains("wanetra", response);
        Assert.Contains("warning, wan", response);
        Assert.Contains("high", response);
        Assert.Contains("hasCredentials", response);
        Assert.DoesNotContain("private-user", response);
        Assert.DoesNotContain("private-password", response);
        Assert.DoesNotContain("private-token", response);
    }
    [Fact]
    public async Task Put_preserves_blank_secret_fields_while_updating_other_settings()
    {
        await using var factory = new WanetraApiFactory();
        var client = factory.CreateClient();
        var first = await client.PutAsJsonAsync("/api/notifications", new NotificationConfigurationUpdateListRequest(
            [new NotificationConfigurationUpdateRequest("ntfy", true, NtfyJson)]));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var updatedJson = """{"serverUrl":"","topic":"","username":"","password":"","token":"","priority":"high"}""";
        var update = await client.PutAsJsonAsync("/api/notifications", new NotificationConfigurationUpdateListRequest(
            [new NotificationConfigurationUpdateRequest("ntfy", true, updatedJson)]));
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        using var scope = factory.Services.CreateScope();
        var stored = await scope.ServiceProvider.GetRequiredService<Wanetra.Infrastructure.Persistence.WanetraDbContext>()
            .NotificationConfigurations.SingleAsync(configuration => configuration.Provider == "ntfy");
        using var document = System.Text.Json.JsonDocument.Parse(stored.ConfigurationJson);
        Assert.Equal("https://ntfy.sh", document.RootElement.GetProperty("serverUrl").GetString());
        Assert.Equal("wanetra", document.RootElement.GetProperty("topic").GetString());
        Assert.Equal("private-password", document.RootElement.GetProperty("password").GetString());
        Assert.Equal("private-token", document.RootElement.GetProperty("token").GetString());
        Assert.Equal("high", document.RootElement.GetProperty("priority").GetString());
    }

    [Fact]
    public async Task Put_blank_json_preserves_stored_secrets()
    {
        await using var factory = new WanetraApiFactory();
        var client = factory.CreateClient();

        var first = await client.PutAsJsonAsync("/api/notifications", new NotificationConfigurationUpdateListRequest(
            [new NotificationConfigurationUpdateRequest("ntfy", true, NtfyJson)]));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await client.PutAsJsonAsync("/api/notifications", new NotificationConfigurationUpdateListRequest(
            [new NotificationConfigurationUpdateRequest("ntfy", false, "")]));
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var saved = await second.Content.ReadFromJsonAsync<NotificationConfigurationListResponse>();

        Assert.NotNull(saved);
        Assert.True(saved.Configurations[0].HasConfiguration);
        Assert.False(saved.Configurations[0].Enabled);
    }

    [Theory]
    [InlineData("bogus", "{}")]
    [InlineData("ntfy", "{}")]
    [InlineData("ntfy", """{"serverUrl":"not-a-url","topic":"t"}""")]
    [InlineData("ntfy", """{"serverUrl":"file:///tmp/ntfy","topic":"t"}""")]
    [InlineData("ntfy", """{"serverUrl":"ftp://ntfy.local","topic":"t"}""")]
    [InlineData("ntfy", """{"serverUrl":"https://ntfy.local/base?token=secret","topic":"t"}""")]
    [InlineData("webhook", """{"url":"not-a-url"}""")]
    [InlineData("webhook", """{"url":"gopher://example.com/hook"}""")]
    [InlineData("webhook", """{"url":"https://user:pass@example.com/hook"}""")]
    [InlineData("webhook", """{"url":"https://example.com/hook#secret"}""")]
    [InlineData("webhook", """{"url":"https://example.com/hook","method":"DELETE"}""")]
    [InlineData("ntfy", "not-json")]
    public async Task Put_rejects_invalid_config(string provider, string json)
    {
        await using var factory = new WanetraApiFactory();
        var client = factory.CreateClient();

        var update = await client.PutAsJsonAsync("/api/notifications", new NotificationConfigurationUpdateListRequest(
            [new NotificationConfigurationUpdateRequest(provider, true, json)]));

        Assert.Equal(HttpStatusCode.BadRequest, update.StatusCode);
        var body = await update.Content.ReadAsStringAsync();
        Assert.DoesNotContain("secret", body);
        var error = await update.Content.ReadFromJsonAsync<ApiError>();
        Assert.NotNull(error);
        Assert.Equal("invalid_request", error.Code);
    }
    [Theory]
    [InlineData("ntfy", """{"serverUrl":"http://127.0.0.1:8081","topic":"wanetra"}""")]
    [InlineData("webhook", """{"url":"http://192.168.1.23:9000/hook"}""")]
    public async Task Put_allows_private_network_notification_urls(string provider, string json)
    {
        await using var factory = new WanetraApiFactory();
        var client = factory.CreateClient();

        var response = await client.PutAsJsonAsync("/api/notifications", new NotificationConfigurationUpdateListRequest(
            [new NotificationConfigurationUpdateRequest(provider, true, json)]));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Test_unknown_provider_returns_bad_request()
    {
        await using var factory = new WanetraApiFactory();
        var client = factory.CreateClient();

        var test = await client.PostAsJsonAsync("/api/notifications/test", new NotificationTestRequest(null, "bogus", "{}"));

        Assert.Equal(HttpStatusCode.BadRequest, test.StatusCode);
    }

    [Fact]
    public async Task Test_missing_target_returns_bad_request()
    {
        await using var factory = new WanetraApiFactory();
        var client = factory.CreateClient();

        var test = await client.PostAsJsonAsync("/api/notifications/test", new NotificationTestRequest(null, null, null));

        Assert.Equal(HttpStatusCode.BadRequest, test.StatusCode);
    }

    [Fact]
    public async Task Test_unknown_id_returns_not_found()
    {
        await using var factory = new WanetraApiFactory();
        var client = factory.CreateClient();

        var test = await client.PostAsJsonAsync("/api/notifications/test", new NotificationTestRequest(999, null, null));

        Assert.Equal(HttpStatusCode.NotFound, test.StatusCode);
    }

    [Fact]
    public async Task Test_unreachable_webhook_returns_502_without_secret_leak()
    {
        await using var factory = new WanetraApiFactory();
        var client = factory.CreateClient();
        var json = """{"url":"http://127.0.0.1:1/hook","headers":{"X-Token":"super-secret-value"}}""";

        var test = await client.PostAsJsonAsync("/api/notifications/test", new NotificationTestRequest(null, "webhook", json));

        Assert.Equal(HttpStatusCode.BadGateway, test.StatusCode);
        var body = await test.Content.ReadAsStringAsync();
        Assert.DoesNotContain("super-secret-value", body);
    }
}
