using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Wanetra.Application.Notifications;
using Wanetra.Domain;
namespace Wanetra.Api.Tests.SpeedTests;

public class NotificationDispatcherTests
{
    [Fact]
    public async Task Opened_dispatch_sets_flag_once_and_second_dispatch_is_noop()
    {
        await using var factory = new WanetraApiFactory();
        var eventId = await SeedEventAsync(factory);
        await SeedConfigAsync(factory, "webhook", """{"url":"https://example.com/hook"}""");

        var sender = await DispatchAsync(factory, NotificationTrigger.Opened, eventId);
        await DispatchAsync(factory, NotificationTrigger.Opened, eventId);

        Assert.Single(sender.Sent);
        Assert.True(await FlagAsync(factory, e => e.NotificationSent));
    }
    [Fact]
    public async Task Failed_provider_does_not_mark_open_notification_sent()
    {
        await using var factory = new WanetraApiFactory();
        var eventId = await SeedEventAsync(factory);
        await SeedConfigAsync(factory, "webhook", "failed");

        var provider = await DispatchAsync(factory, NotificationTrigger.Opened, eventId, _ => true);

        Assert.Single(provider.Attempts);
        Assert.Empty(provider.Sent);
        Assert.False(await FlagAsync(factory, e => e.NotificationSent));
    }

    [Fact]
    public async Task Unknown_provider_skips_without_setting_flag()
    {
        await using var factory = new WanetraApiFactory();
        var eventId = await SeedEventAsync(factory);
        await SeedConfigAsync(factory, "bogus", "{}");

        await DispatchAsync(factory, NotificationTrigger.Opened, eventId);

        Assert.False(await FlagAsync(factory, e => e.NotificationSent));
    }

    [Fact]
    public async Task No_enabled_configs_leaves_flags_untouched()
    {
        await using var factory = new WanetraApiFactory();
        var eventId = await SeedEventAsync(factory);

        await DispatchAsync(factory, NotificationTrigger.Opened, eventId);

        Assert.False(await FlagAsync(factory, e => e.NotificationSent));
    }
    [Fact]
    public async Task All_provider_failures_leave_notification_pending()
    {
        await using var factory = new WanetraApiFactory();
        var eventId = await SeedEventAsync(factory);
        await SeedConfigAsync(factory, "webhook", "failed");
        await SeedConfigAsync(factory, "webhook", "also-failed");

        var provider = await DispatchAsync(factory, NotificationTrigger.Opened, eventId, _ => true);

        Assert.Equal(2, provider.Attempts.Count);
        Assert.False(await FlagAsync(factory, e => e.NotificationSent));
    }

    [Fact]
    public async Task Successful_provider_marks_notification_sent_even_when_another_provider_fails()
    {
        await using var factory = new WanetraApiFactory();
        var eventId = await SeedEventAsync(factory);
        await SeedConfigAsync(factory, "webhook", "failed");
        await SeedConfigAsync(factory, "webhook", "successful");

        var provider = await DispatchAsync(
            factory,
            NotificationTrigger.Opened,
            eventId,
            configuration => configuration == "failed");

        Assert.Equal(2, provider.Attempts.Count);
        Assert.Single(provider.Sent);
        Assert.True(await FlagAsync(factory, e => e.NotificationSent));
    }

    [Fact]
    public async Task Recovered_dispatch_sets_recovery_flag()
    {
        await using var factory = new WanetraApiFactory();
        var eventId = await SeedEventAsync(factory, DegradationStatus.Recovered);
        await SeedConfigAsync(factory, "webhook", """{"url":"https://example.com/hook"}""");

        await DispatchAsync(factory, NotificationTrigger.Recovered, eventId);

        Assert.True(await FlagAsync(factory, e => e.RecoveryNotificationSent));
        Assert.False(await FlagAsync(factory, e => e.NotificationSent));
    }
    [Fact]
    public async Task Failed_recovery_notification_remains_pending_for_retry()
    {
        await using var factory = new WanetraApiFactory();
        var eventId = await SeedEventAsync(
            factory,
            DegradationStatus.Recovered,
            notificationSent: true);
        await SeedConfigAsync(factory, "webhook", "failed");

        var failed = await DispatchAsync(factory, NotificationTrigger.Recovered, eventId, _ => true);
        Assert.Empty(failed.Sent);
        Assert.False(await FlagAsync(factory, e => e.RecoveryNotificationSent));

        var retry = await DispatchPendingAsync(factory);

        Assert.Single(retry.Sent);
        Assert.True(await FlagAsync(factory, e => e.RecoveryNotificationSent));
    }
    [Fact]
    public async Task Recovered_event_retries_open_before_sending_recovery()
    {
        await using var factory = new WanetraApiFactory();
        await SeedEventAsync(factory, DegradationStatus.Recovered);
        await SeedConfigAsync(factory, "webhook", "configured");

        var failedOpen = await DispatchPendingAsync(factory, _ => true);
        Assert.Single(failedOpen.Attempts);
        Assert.False(await FlagAsync(factory, e => e.NotificationSent));
        Assert.False(await FlagAsync(factory, e => e.RecoveryNotificationSent));

        var retry = await DispatchPendingAsync(factory);

        Assert.Equal(2, retry.Sent.Count);
        Assert.True(await FlagAsync(factory, e => e.NotificationSent));
        Assert.True(await FlagAsync(factory, e => e.RecoveryNotificationSent));
    }

    [Fact]
    public async Task Persisted_open_event_with_failed_delivery_is_retried()
    {
        await using var factory = new WanetraApiFactory();
        var eventId = await SeedEventAsync(factory);
        await SeedConfigAsync(factory, "webhook", "failed");

        var failed = await DispatchAsync(factory, NotificationTrigger.Opened, eventId, _ => true);
        Assert.False(await FlagAsync(factory, e => e.NotificationSent));

        var retry = await DispatchPendingAsync(factory);

        Assert.Single(retry.Sent);
        Assert.True(await FlagAsync(factory, e => e.NotificationSent));
    }

    [Fact]
    public async Task Ntfy_sends_put_with_title_and_auth()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK);
        var provider = new NtfyNotificationProvider(new HttpClient(handler));
        var message = NotificationMessageBuilder.Test("ntfy");

        await provider.SendAsync(
            message,
            """{"serverUrl":"https://ntfy.sh","topic":"wanetra","token":"secret","priority":"high","tags":["warning"]}""",
            CancellationToken.None);

        Assert.Single(handler.Requests);
        var request = handler.Requests[0];
        Assert.Equal(HttpMethod.Put, request.Method);
        Assert.Equal("https://ntfy.sh/wanetra", request.RequestUri!.ToString());
        Assert.Equal("secret", request.Headers.Authorization!.Parameter);
        Assert.True(request.Headers.Contains("Title"));
        Assert.True(request.Headers.Contains("Priority"));
    }

    [Fact]
    public async Task Webhook_payload_excludes_secrets()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK);
        var provider = new WebhookNotificationProvider(new HttpClient(handler));
        var message = new NotificationMessage("t", "b", "connection.degraded", DateTime.UtcNow, 412, 823, -49.9);

        await provider.SendAsync(
            message,
            """{"url":"https://example.com/hook","headers":{"X-Token":"secret"}}""",
            CancellationToken.None);

        Assert.Single(handler.Bodies);
        Assert.Contains("connection.degraded", handler.Bodies[0]);
        Assert.DoesNotContain("secret", handler.Bodies[0]);
        Assert.Equal("secret", handler.Requests[0].Headers.GetValues("X-Token").Single());
    }

    [Fact]
    public async Task Webhook_non_success_throws()
    {
        var provider = new WebhookNotificationProvider(new HttpClient(new RecordingHandler(HttpStatusCode.InternalServerError)));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            provider.SendAsync(
                NotificationMessageBuilder.Test("webhook"),
                """{"url":"https://example.com/hook"}""",
                CancellationToken.None));
    }

    private static async Task<long> SeedEventAsync(
        WanetraApiFactory factory,
        DegradationStatus status = DegradationStatus.Active,
        bool notificationSent = false)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Infrastructure.Persistence.WanetraDbContext>();
        var degradationEvent = new DegradationEvent
        {
            StartedAt = DateTime.UtcNow,
            EndedAt = status == DegradationStatus.Recovered ? DateTime.UtcNow : null,
            Status = status,
            Reason = "download below threshold",
            BaselineDownloadMbps = 823,
            WorstDownloadMbps = 412,
            NotificationSent = notificationSent,
        };
        db.DegradationEvents.Add(degradationEvent);
        await db.SaveChangesAsync();
        return degradationEvent.Id;
    }
    private static async Task SeedConfigAsync(WanetraApiFactory factory, string providerName, string json)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Infrastructure.Persistence.WanetraDbContext>();
        db.NotificationConfigurations.Add(new NotificationConfiguration
        {
            Provider = providerName,
            Enabled = true,
            ConfigurationJson = json,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
    }

    private static async Task<RecordingProvider> DispatchAsync(
        WanetraApiFactory factory,
        NotificationTrigger trigger,
        long eventId,
        Func<string, bool>? shouldFail = null)
    {
        using var scope = factory.Services.CreateScope();
        var recorder = new RecordingProvider(shouldFail);
        var dispatcher = new NotificationDispatcher(
            scope.ServiceProvider.GetRequiredService<INotificationConfigurationRepository>(),
            scope.ServiceProvider.GetRequiredService<IAlertStateRepository>(),
            [recorder],
            scope.ServiceProvider.GetRequiredService<ILogger<NotificationDispatcher>>());
        await dispatcher.DispatchAsync(trigger, eventId, CancellationToken.None);
        return recorder;
    }

    private static async Task<RecordingProvider> DispatchPendingAsync(
        WanetraApiFactory factory,
        Func<string, bool>? shouldFail = null)
    {
        using var scope = factory.Services.CreateScope();
        var recorder = new RecordingProvider(shouldFail);
        var dispatcher = new NotificationDispatcher(
            scope.ServiceProvider.GetRequiredService<INotificationConfigurationRepository>(),
            scope.ServiceProvider.GetRequiredService<IAlertStateRepository>(),
            [recorder],
            scope.ServiceProvider.GetRequiredService<ILogger<NotificationDispatcher>>());
        await dispatcher.DispatchPendingAsync(CancellationToken.None);
        return recorder;
    }
    private static async Task<bool> FlagAsync(WanetraApiFactory factory, Func<DegradationEvent, bool> pick)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Infrastructure.Persistence.WanetraDbContext>();
        return pick(await db.DegradationEvents.SingleAsync());
    }

    private sealed class RecordingProvider(Func<string, bool>? shouldFail = null) : INotificationProvider
    {
        public string Name => "webhook";
        public List<NotificationMessage> Sent { get; } = [];
        public List<string> Attempts { get; } = [];

        public Task SendAsync(NotificationMessage message, string configurationJson, CancellationToken cancellationToken)
        {
            Attempts.Add(configurationJson);
            if (shouldFail?.Invoke(configurationJson) == true)
            {
                throw new InvalidOperationException("provider failure");
            }

            Sent.Add(message);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingHandler(HttpStatusCode status) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];
        public List<string> Bodies { get; } = [];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            Bodies.Add(request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken));
            return new HttpResponseMessage(status);
        }
    }
}
