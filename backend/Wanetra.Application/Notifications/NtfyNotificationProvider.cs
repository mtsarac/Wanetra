using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Wanetra.Application.Notifications;

public sealed record NtfyConfiguration(
    string ServerUrl,
    string Topic,
    string? Username,
    string? Password,
    string? Token,
    string Priority,
    string[] Tags)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static NtfyConfiguration Parse(string configurationJson)
    {
        NtfyOptions? options;
        try
        {
            options = JsonSerializer.Deserialize<NtfyOptions>(configurationJson, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new ArgumentException("Invalid ntfy configuration JSON.", ex);
        }

        if (options is null || string.IsNullOrWhiteSpace(options.ServerUrl) || string.IsNullOrWhiteSpace(options.Topic))
        {
            throw new ArgumentException("ntfy configuration requires serverUrl and topic.");
        }

        if (!Uri.TryCreate(options.ServerUrl.Trim(), UriKind.Absolute, out _))
        {
            throw new ArgumentException("ntfy serverUrl must be an absolute URL.");
        }

        if (options.Topic.Trim().Contains('/') || options.Topic.Trim().Contains(' '))
        {
            throw new ArgumentException("ntfy topic must not contain slashes or spaces.");
        }

        return new NtfyConfiguration(
            options.ServerUrl.Trim().TrimEnd('/'),
            options.Topic.Trim(),
            string.IsNullOrWhiteSpace(options.Username) ? null : options.Username.Trim(),
            string.IsNullOrWhiteSpace(options.Password) ? null : options.Password.Trim(),
            string.IsNullOrWhiteSpace(options.Token) ? null : options.Token.Trim(),
            string.IsNullOrWhiteSpace(options.Priority) ? "default" : options.Priority.Trim(),
            options.Tags ?? []);
    }

    private sealed class NtfyOptions
    {
        public string? ServerUrl { get; set; }
        public string? Topic { get; set; }
        public string? Username { get; set; }
        public string? Password { get; set; }
        public string? Token { get; set; }
        public string? Priority { get; set; }
        public string[]? Tags { get; set; }
    }
}

public sealed class NtfyNotificationProvider(HttpClient httpClient) : INotificationProvider
{
    public string Name => "ntfy";

    public async Task SendAsync(NotificationMessage message, string configurationJson, CancellationToken cancellationToken)
    {
        var config = NtfyConfiguration.Parse(configurationJson);
        using var request = new HttpRequestMessage(HttpMethod.Put, $"{config.ServerUrl}/{config.Topic}")
        {
            Content = new StringContent(message.Body, Encoding.UTF8, "text/plain"),
        };
        request.Headers.TryAddWithoutValidation("Title", message.Title);
        request.Headers.TryAddWithoutValidation("Priority", config.Priority);
        if (config.Tags.Length > 0)
        {
            request.Headers.TryAddWithoutValidation("Tags", string.Join(',', config.Tags));
        }

        if (config.Token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.Token);
        }
        else if (config.Username is not null && config.Password is not null)
        {
            var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{config.Username}:{config.Password}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        }

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"ntfy returned {(int)response.StatusCode}.");
        }
    }
}
