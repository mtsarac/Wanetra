namespace Wanetra.Infrastructure.Probes;

internal readonly record struct DnsReply(ushort Id, int ResponseCode, int AnswerCount);

// Just enough of RFC 1035 to ask a resolver for an A record and read the verdict.
internal static class DnsWire
{
    public static byte[] BuildQuery(string name, ushort id)
    {
        var labels = name.TrimEnd('.').Split('.');
        if (labels.Any(label => label.Length is < 1 or > 63) || name.TrimEnd('.').Length > 253)
        {
            throw new ArgumentException("Not a valid DNS name.", nameof(name));
        }

        var query = new List<byte>(32)
        {
            (byte)(id >> 8), (byte)id,
            0x01, 0x00, // recursion desired
            0x00, 0x01, // one question
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        };
        foreach (var label in labels)
        {
            query.Add((byte)label.Length);
            query.AddRange(System.Text.Encoding.ASCII.GetBytes(label));
        }

        query.Add(0);
        query.AddRange([0x00, 0x01, 0x00, 0x01]); // type A, class IN
        return [.. query];
    }

    public static DnsReply? ParseReply(ReadOnlySpan<byte> data)
    {
        if (data.Length < 12)
        {
            return null;
        }

        var flags = (data[2] << 8) | data[3];
        if ((flags & 0x8000) == 0)
        {
            return null; // not a response
        }

        return new DnsReply((ushort)((data[0] << 8) | data[1]), flags & 0x000F, (data[6] << 8) | data[7]);
    }
}
