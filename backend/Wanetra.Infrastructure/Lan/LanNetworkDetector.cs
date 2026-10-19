using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Wanetra.Application.Lan;

namespace Wanetra.Infrastructure.Lan;

internal sealed record InterfaceAddress(string Name, IPAddress Address, int PrefixLength, bool IsUp, byte[]? Mac);

public sealed class LanNetworkDetector : ILanNetworkDetector
{
    // Virtual and tunnel interfaces: they are not a LAN the owner means.
    private static readonly string[] IgnoredPrefixes = ["docker", "br-", "veth", "virbr", "cni", "flannel", "cali", "tailscale", "zt", "wg", "tun", "tap", "lo"];

    // Docker gives containers on its bridge networks a MAC that starts with 02:42.
    private static bool IsContainerMac(byte[]? mac) => mac is { Length: 6 } && mac[0] == 0x02 && mac[1] == 0x42;

    public NetworkDetection Detect() => Select(Interfaces());

    internal static NetworkDetection Select(IEnumerable<InterfaceAddress> interfaces)
    {
        var networks = new List<DetectedNetwork>();
        var skippedContainerNetwork = false;
        foreach (var candidate in interfaces)
        {
            if (!candidate.IsUp
                || candidate.Address.AddressFamily != AddressFamily.InterNetwork
                || IPAddress.IsLoopback(candidate.Address)
                || IgnoredPrefixes.Any(prefix => candidate.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            if (candidate.PrefixLength > 30)
            {
                continue;
            }

            // A /16 or /8 would mean tens of thousands of addresses; scan the /24 the host sits in instead.
            var prefix = candidate.PrefixLength >= CidrRange.MinPrefixLength ? candidate.PrefixLength : 24;
            var range = CidrRange.Of(candidate.Address, prefix);
            if (range.Problem() is not null)
            {
                continue;
            }

            if (IsContainerMac(candidate.Mac))
            {
                skippedContainerNetwork = true;
                continue;
            }

            if (networks.All(network => network.Range != range))
            {
                networks.Add(new DetectedNetwork(range, candidate.Name, candidate.Address.ToString()));
            }
        }

        return new NetworkDetection(networks, networks.Count == 0 && skippedContainerNetwork);
    }

    internal static IEnumerable<InterfaceAddress> Interfaces()
    {
        foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
        {
            var mac = networkInterface.GetPhysicalAddress().GetAddressBytes();
            foreach (var unicast in networkInterface.GetIPProperties().UnicastAddresses)
            {
                if (unicast.Address.AddressFamily == AddressFamily.InterNetwork)
                {
                    yield return new InterfaceAddress(
                        networkInterface.Name,
                        unicast.Address,
                        unicast.PrefixLength,
                        networkInterface.OperationalStatus == OperationalStatus.Up,
                        mac);
                }
            }
        }
    }
}
