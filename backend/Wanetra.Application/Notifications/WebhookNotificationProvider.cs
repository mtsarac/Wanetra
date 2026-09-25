using System.Net.Http.Json;
using System.Text.Json;

namespace Wanetra.Application.Notifications;

public sealed record WebhookConfiguration(
    string Url,
    string Method,
    Dictionary<string, string> Headers)
{
    private static readonly HashSet<string> AllowedMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "GET", "POST", "PUT",
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static WebhookConfiguration Parse(string configurationJson)
    {
        WebhookOptions? options;
        try
        {
            options = JsonSerializer.Deserialize<WebhookOptions>(configurationJson, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new ArgumentException("Invalid webhook configuration JSON.", ex);
        }

        if (options is null || string.IsNullOrWhiteSpace(options.Url))
        {
            throw new ArgumentException("Webhook configuration requires url.");
        }

        var url = NotificationUrlValidator.RequireHttpUrl(options.Url, "Webhook URL");

        var method = string.IsNullOrWhiteSpace(options.Method) ? "POST" : options.Method.Trim().ToUpperInvariant();
        if (!AllowedMethods.Contains(method))
        {
            throw new ArgumentException("Webhook method must be GET, POST, or PUT.");
        }

        return new WebhookConfiguration(url, method, options.Headers ?? []);
    }

    private sealed class WebhookOptions
    {
        public string? Url { get; set; }
        public string? Method { get; set; }
        public Dictionary<string, string>? Headers { get; set; }
    }
}

public sealed class WebhookNotificationProvider(HttpClient httpClient) : INotificationProvider
{
    public string Name => "webhook";

    public async Task SendAsync(NotificationMessage message, string configurationJson, CancellationToken cancellationToken)
    {
        var config = WebhookConfiguration.Parse(configurationJson);
        using var request = new HttpRequestMessage(new HttpMethod(config.Method), config.Url);
        foreach (var (name, value) in config.Headers)
        {
            request.Headers.TryAddWithoutValidation(name, value);
        }

        if (!string.Equals(config.Method, "GET", StringComparison.OrdinalIgnoreCase))
        {
            request.Content = JsonContent.Create(new
            {
                @event = message.Event,
                timestamp = message.Timestamp,
                downloadMbps = message.DownloadMbps,
                baselineDownloadMbps = message.BaselineDownloadMbps,
                degradationPercent = message.DegradationPercent,
            });
        }

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Webhook returned {(int)response.StatusCode}.");
        }
    }
}
