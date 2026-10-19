namespace Wanetra.Infrastructure.Lan;

internal static class HostNames
{
    private static readonly string[] Suffixes = [".local", ".lan", ".home", ".localdomain", ".home.arpa", ".fritz.box", ".internal"];

    // Device-chosen names end up in the UI and in notifications: strip the search domain and anything unprintable.
    public static string? Clean(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var cleaned = new string(name.Trim().TrimEnd('.').Where(c => !char.IsControl(c)).ToArray());
        if (Suffixes.Any(suffix => cleaned.Equals(suffix, StringComparison.OrdinalIgnoreCase)))
        {
            return null;
        }

        foreach (var suffix in Suffixes)
        {
            if (cleaned.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) && cleaned.Length > suffix.Length)
            {
                cleaned = cleaned[..^suffix.Length];
                break;
            }
        }

        cleaned = cleaned.Trim();
        if (cleaned.Length == 0)
        {
            return null;
        }

        return cleaned.Length > 64 ? cleaned[..64] : cleaned;
    }
}
