using System.Net;
using System.Net.Http.Json;
using Wanetra.Api.Contracts;

namespace Wanetra.Api.Tests.SpeedTests;

public class NotificationApiTests
{
    private const string NtfyJson = """{"serverUrl":"https://ntfy.sh","topic":"wanetra"}""";
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
    [InlineData("webhook", """{"url":"not-a-url"}""")]
    [InlineData("webhook", """{"url":"https://example.com/hook","method":"DELETE"}""")]
    [InlineData("ntfy", "not-json")]
    public async Task Put_rejects_invalid_config(string provider, string json)
    {
        await using var factory = new WanetraApiFactory();
        var client = factory.CreateClient();

        var update = await client.PutAsJsonAsync("/api/notifications", new NotificationConfigurationUpdateListRequest(
            [new NotificationConfigurationUpdateRequest(provider, true, json)]));

        Assert.Equal(HttpStatusCode.BadRequest, update.StatusCode);
        var error = await update.Content.ReadFromJsonAsync<ApiError>();
        Assert.NotNull(error);
        Assert.Equal("invalid_request", error.Code);
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
