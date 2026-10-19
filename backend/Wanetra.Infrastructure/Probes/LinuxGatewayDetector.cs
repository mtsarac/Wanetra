using System.Globalization;
using System.Net;
using Wanetra.Application.Probes;

namespace Wanetra.Infrastructure.Probes;

public sealed class LinuxGatewayDetector(string routeTablePath = "/proc/net/route") : IGatewayDetector
{
    public string? DetectGateway()
    {
        try
        {
            return File.Exists(routeTablePath) ? Parse(File.ReadAllText(routeTablePath)) : null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    // Picks the default route with the lowest metric. Columns: Iface Destination Gateway Flags RefCnt Use Metric Mask ...
    public static string? Parse(string routeTable)
    {
        const int RouteUp = 0x1;
        const int RouteGateway = 0x2;

        string? best = null;
        var bestMetric = int.MaxValue;
        foreach (var line in routeTable.Split('\n').Skip(1))
        {
            var columns = line.Split(['\t', ' '], StringSplitOptions.RemoveEmptyEntries);
            if (columns.Length < 8
                || columns[1] != "00000000"
                || !int.TryParse(columns[3], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var flags)
                || (flags & (RouteUp | RouteGateway)) != (RouteUp | RouteGateway)
                || !uint.TryParse(columns[2], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var gateway)
                || gateway == 0
                || !int.TryParse(columns[6], CultureInfo.InvariantCulture, out var metric)
                || metric >= bestMetric)
            {
                continue;
            }

            var address = new IPAddress([(byte)gateway, (byte)(gateway >> 8), (byte)(gateway >> 16), (byte)(gateway >> 24)]);
            best = address.ToString();
            bestMetric = metric;
        }

        return best;
    }
}
