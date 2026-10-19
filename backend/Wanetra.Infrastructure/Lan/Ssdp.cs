using System.Net;
using System.Text;
using System.Xml;

namespace Wanetra.Infrastructure.Lan;

internal sealed record SsdpResponse(string? Location, string? Server);

// UPnP discovery. Routers, TVs, printers and speakers answer a multicast search with where their description lives.
internal static class Ssdp
{
    public static readonly IPEndPoint Group = new(IPAddress.Parse("239.255.255.250"), 1900);

    public static byte[] BuildSearch() => Encoding.ASCII.GetBytes(
        "M-SEARCH * HTTP/1.1\r\nHOST: 239.255.255.250:1900\r\nMAN: \"ssdp:discover\"\r\nMX: 2\r\nST: ssdp:all\r\n\r\n");

    public static SsdpResponse? ParseResponse(ReadOnlySpan<byte> data)
    {
        if (data.Length is 0 or > 4096)
        {
            return null;
        }

        var lines = Encoding.ASCII.GetString(data).Split("\r\n");
        if (lines.Length == 0 || !lines[0].StartsWith("HTTP/1.1 200", StringComparison.Ordinal))
        {
            return null;
        }

        string? Header(string name) => lines
            .Skip(1)
            .Select(line => line.Split(':', 2))
            .Where(parts => parts.Length == 2 && parts[0].Equals(name, StringComparison.OrdinalIgnoreCase))
            .Select(parts => parts[1].Trim())
            .FirstOrDefault();

        return new SsdpResponse(Header("LOCATION"), Header("SERVER"));
    }

    // The description document is fetched from the device, so it is untrusted: no DTDs, no external resources.
    public static string? ParseFriendlyName(string xml)
    {
        try
        {
            using var reader = XmlReader.Create(
                new StringReader(xml),
                new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 65536 });
            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "friendlyName")
                {
                    var name = reader.ReadElementContentAsString().Trim();
                    return name.Length > 0 ? name : null;
                }
            }
        }
        catch (XmlException)
        {
        }

        return null;
    }
}
