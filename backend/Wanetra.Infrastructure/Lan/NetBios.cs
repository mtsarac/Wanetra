using Wanetra.Infrastructure.Probes;

namespace Wanetra.Infrastructure.Lan;

// NetBIOS node status (RFC 1002): asks a Windows or Samba host for the names it has registered.
internal static class NetBios
{
    public const int Port = 137;

    public static byte[] BuildNodeStatusQuery(ushort id)
    {
        var query = new List<byte> { (byte)(id >> 8), (byte)id, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0 };
        query.Add(0x20);
        // The wildcard name "*" padded with zeros, in first-level encoding.
        query.AddRange("CKAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA"u8.ToArray());
        query.AddRange([0x00, 0x00, 0x21, 0x00, 0x01]);
        return [.. query];
    }

    public static string? ParseComputerName(ReadOnlySpan<byte> message)
    {
        if (message.Length < 12 || (message[2] & 0x80) == 0 || ((message[6] << 8) | message[7]) < 1)
        {
            return null;
        }

        var position = 12;
        if (DnsWire.ReadName(message, ref position) is null || position + 11 > message.Length)
        {
            return null;
        }

        if (((message[position] << 8) | message[position + 1]) != 0x0021)
        {
            return null;
        }

        position += 10;
        var count = message[position++];
        for (var i = 0; i < count && position + 18 <= message.Length; i++, position += 18)
        {
            var suffix = message[position + 15];
            var flags = (message[position + 16] << 8) | message[position + 17];
            var isGroup = (flags & 0x8000) != 0;
            if (suffix == 0x00 && !isGroup)
            {
                var name = System.Text.Encoding.ASCII.GetString(message.Slice(position, 15)).Trim();
                return name.Length > 0 ? name : null;
            }
        }

        return null;
    }
}
