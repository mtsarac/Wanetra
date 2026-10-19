using System.Globalization;
using System.Net;
using Wanetra.Application.Lan;

namespace Wanetra.Infrastructure.Lan;

internal readonly record struct ArpEntry(IPAddress Ip, string Mac, string Interface);

// The kernel's neighbour table. After a packet goes to an address on the local network the kernel has asked
// "who has this IP?" and the table holds the answer, which is how devices are found without raw sockets.
internal static class ArpTable
{
    private const int FlagComplete = 0x2;

    public static IReadOnlyList<ArpEntry> Parse(string text)
    {
        var entries = new List<ArpEntry>();
        foreach (var line in text.Split('\n').Skip(1))
        {
            // IP address  HW type  Flags  HW address  Mask  Device
            var columns = line.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
            if (columns.Length < 6
                || !IPAddress.TryParse(columns[0], out var ip)
                || !int.TryParse(columns[2].Replace("0x", string.Empty), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var flags)
                || (flags & FlagComplete) == 0
                || MacAddress.Normalize(columns[3]) is not { } mac)
            {
                continue;
            }

            entries.Add(new ArpEntry(ip, mac, columns[5]));
        }

        return entries;
    }
}
