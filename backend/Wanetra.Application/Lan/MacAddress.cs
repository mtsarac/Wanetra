using System.Globalization;

namespace Wanetra.Application.Lan;

public static class MacAddress
{
    // "AA-BB-CC-DD-EE-FF", "aabb.ccdd.eeff" and "aabbccddeeff" all become "aa:bb:cc:dd:ee:ff".
    // Null for anything that is not the address of a single device.
    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        value = value.Trim();
        var hex = new string(value.Where(char.IsAsciiHexDigit).ToArray());
        if (hex.Length != 12 || value.Length != hex.Length + value.Count(c => c is ':' or '-' or '.'))
        {
            return null;
        }

        var bytes = Enumerable.Range(0, 6)
            .Select(i => byte.Parse(hex.AsSpan(i * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture))
            .ToArray();
        if (bytes.All(b => b == 0) || bytes.All(b => b == 0xFF) || (bytes[0] & 0x01) != 0)
        {
            return null;
        }

        return string.Join(':', bytes.Select(b => b.ToString("x2", CultureInfo.InvariantCulture)));
    }

    // Set on addresses a phone or laptop made up for itself (locally administered), which change per network.
    public static bool IsRandomized(string mac) =>
        (byte.Parse(mac.AsSpan(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture) & 0x02) != 0;

    // The registry key: the first three bytes in upper case without separators.
    public static string Prefix(string mac) => mac.Replace(":", string.Empty)[..6].ToUpperInvariant();
}
