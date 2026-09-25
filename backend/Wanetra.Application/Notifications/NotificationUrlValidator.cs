namespace Wanetra.Application.Notifications;

internal static class NotificationUrlValidator
{
    public static string RequireHttpUrl(string value, string settingName, bool allowQuery = true)
    {
        var trimmed = value.Trim();
        if (trimmed.Any(char.IsControl)
            || !Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            || !uri.IsWellFormedOriginalString()
            || string.IsNullOrWhiteSpace(uri.Host)
            || !string.IsNullOrEmpty(uri.UserInfo)
            || !string.IsNullOrEmpty(uri.Fragment)
            || (!allowQuery && !string.IsNullOrEmpty(uri.Query)))
        {
            throw new ArgumentException(
                $"{settingName} must be an absolute HTTP or HTTPS URL without user information or a fragment.");
        }

        return trimmed;
    }
}
