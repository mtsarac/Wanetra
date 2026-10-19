namespace Wanetra.Infrastructure.Probes;

internal readonly record struct DnsReply(ushort Id, int ResponseCode, int AnswerCount);

// Just enough of RFC 1035 to ask a resolver for an A record and read the verdict.
internal static class DnsWire
{
    public const ushort TypeA = 1;
    public const ushort TypePtr = 12;

    public static byte[] BuildQuery(string name, ushort id, ushort type = TypeA)
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
        query.AddRange([(byte)(type >> 8), (byte)type, 0x00, 0x01]); // class IN
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

    // The first PTR record in the answer section, e.g. the name a device gives itself over mDNS.
    public static string? ReadFirstPtr(ReadOnlySpan<byte> message)
    {
        if (message.Length < 12 || (message[3] & 0x0F) != 0)
        {
            return null;
        }

        var questions = (message[4] << 8) | message[5];
        var answers = (message[6] << 8) | message[7];
        var position = 12;
        for (var i = 0; i < questions; i++)
        {
            if (ReadName(message, ref position) is null)
            {
                return null;
            }

            position += 4;
        }

        for (var i = 0; i < answers; i++)
        {
            if (ReadName(message, ref position) is null || position + 10 > message.Length)
            {
                return null;
            }

            var type = (message[position] << 8) | message[position + 1];
            var length = (message[position + 8] << 8) | message[position + 9];
            position += 10;
            if (position + length > message.Length)
            {
                return null;
            }

            if (type == TypePtr)
            {
                var rdata = position;
                return ReadName(message, ref rdata);
            }

            position += length;
        }

        return null;
    }

    // Reads a possibly compressed name and moves the position past it.
    public static string? ReadName(ReadOnlySpan<byte> message, ref int position)
    {
        var labels = new List<string>();
        var cursor = position;
        var resume = -1;
        for (var jumps = 0; jumps < 64; jumps++)
        {
            if (cursor >= message.Length)
            {
                return null;
            }

            var length = message[cursor];
            if (length == 0)
            {
                position = resume >= 0 ? resume : cursor + 1;
                return string.Join('.', labels);
            }

            if ((length & 0xC0) == 0xC0)
            {
                if (cursor + 1 >= message.Length)
                {
                    return null;
                }

                resume = resume >= 0 ? resume : cursor + 2;
                cursor = ((length & 0x3F) << 8) | message[cursor + 1];
                continue;
            }

            if ((length & 0xC0) != 0 || cursor + 1 + length > message.Length)
            {
                return null;
            }

            labels.Add(System.Text.Encoding.ASCII.GetString(message.Slice(cursor + 1, length)));
            cursor += 1 + length;
        }

        return null;
    }
}
