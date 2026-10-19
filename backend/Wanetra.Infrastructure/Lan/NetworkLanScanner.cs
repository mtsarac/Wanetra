using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Wanetra.Application.Lan;
using Wanetra.Infrastructure.Probes;

namespace Wanetra.Infrastructure.Lan;

// Finds devices without raw sockets or extra privileges:
//   1. send one datagram to every address, which makes the kernel ask the LAN "who has this IP?";
//   2. read the answers from the kernel's neighbour table;
//   3. ask each device that answered what it calls itself (mDNS, NetBIOS, reverse DNS, UPnP).
public sealed class NetworkLanScanner(ILogger<NetworkLanScanner> logger) : ILanScanner
{
    private const int ChunkSize = 254;
    private const int DiscardPort = 9;
    private const int MaxDescriptionLookups = 64;
    private const int MaxDescriptionBytes = 65536;

    private static readonly HttpClient Http = new(new SocketsHttpHandler { UseProxy = false, AllowAutoRedirect = false })
    {
        Timeout = TimeSpan.FromSeconds(2),
        MaxResponseContentBufferSize = MaxDescriptionBytes,
    };

    internal string ArpPath { get; init; } = "/proc/net/arp";

    // How long the kernel gets to resolve (or give up on) the addresses before the table is read.
    internal TimeSpan Settle { get; init; } = TimeSpan.FromSeconds(10);

    internal TimeSpan EnrichTimeout { get; init; } = TimeSpan.FromMilliseconds(1500);

    internal int MdnsPort { get; init; } = 5353;

    internal int NetBiosPort { get; init; } = NetBios.Port;

    internal bool UseSsdp { get; init; } = true;

    public async Task<LanScanResult> ScanAsync(IReadOnlyList<CidrRange> ranges, CancellationToken cancellationToken)
    {
        if (!File.Exists(ArpPath))
        {
            return new LanScanResult([], "The kernel's ARP table (/proc/net/arp) is not available, so LAN scanning only works on Linux.");
        }

        var table = new Dictionary<string, ArpEntry>(StringComparer.Ordinal);
        foreach (var chunk in ranges.SelectMany(range => range.Hosts()).Chunk(ChunkSize))
        {
            Touch(chunk);
            await Task.Delay(Settle, cancellationToken);
            foreach (var entry in ArpTable.Parse(await File.ReadAllTextAsync(ArpPath, cancellationToken)))
            {
                if (ranges.Any(range => range.Contains(entry.Ip)))
                {
                    table[entry.Ip.ToString()] = entry;
                }
            }
        }

        var hosts = table.Values
            .Select(entry => new Candidate(entry.Ip, entry.Mac))
            .Concat(OwnAddresses(ranges).Where(own => !table.ContainsKey(own.Ip.ToString())))
            .OrderBy(candidate => candidate.Ip.GetAddressBytes(), Comparer<byte[]>.Create((a, b) => a.AsSpan().SequenceCompareTo(b)))
            .ToList();

        var ssdp = UseSsdp ? await DiscoverUpnpAsync(ranges, hosts, cancellationToken) : [];
        var discovered = new List<DiscoveredHost>(hosts.Count);
        var gate = new object();
        await Parallel.ForEachAsync(hosts, new ParallelOptions { MaxDegreeOfParallelism = 32, CancellationToken = cancellationToken },
            async (candidate, token) =>
            {
                var hostname = candidate.OwnName ?? await NameAsync(candidate.Ip, token);
                ssdp.TryGetValue(candidate.Ip.ToString(), out var description);
                lock (gate)
                {
                    discovered.Add(new DiscoveredHost(candidate.Ip.ToString(), candidate.Mac, hostname, description));
                }
            });

        return new LanScanResult([.. discovered.OrderBy(host => IPAddress.Parse(host.Ip).GetAddressBytes(), Comparer<byte[]>.Create((a, b) => a.AsSpan().SequenceCompareTo(b)))]);
    }

    private void Touch(IEnumerable<IPAddress> addresses)
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        byte[] payload = [0];
        foreach (var address in addresses)
        {
            try
            {
                socket.SendTo(payload, new IPEndPoint(address, DiscardPort));
            }
            catch (SocketException exception)
            {
                logger.LogDebug("Could not send to {Address}: {Error}", address, exception.SocketErrorCode);
            }
        }
    }

    // This machine is not in its own neighbour table.
    private static IEnumerable<Candidate> OwnAddresses(IReadOnlyList<CidrRange> ranges)
    {
        foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (networkInterface.OperationalStatus != OperationalStatus.Up
                || MacAddress.Normalize(string.Join(':', networkInterface.GetPhysicalAddress().GetAddressBytes().Select(b => b.ToString("x2")))) is not { } mac)
            {
                continue;
            }

            foreach (var unicast in networkInterface.GetIPProperties().UnicastAddresses)
            {
                if (ranges.Any(range => range.Contains(unicast.Address)))
                {
                    yield return new Candidate(unicast.Address, mac, HostNames.Clean(Dns.GetHostName()));
                }
            }
        }
    }

    // The first answer in order of how much the device itself is to be believed.
    private async Task<string?> NameAsync(IPAddress address, CancellationToken cancellationToken)
    {
        var mdns = MdnsNameAsync(address, cancellationToken);
        var netbios = NetBiosNameAsync(address, cancellationToken);
        var reverse = ReverseDnsAsync(address, cancellationToken);
        await Task.WhenAll(mdns, netbios, reverse);
        return await mdns ?? await netbios ?? await reverse;
    }

    private async Task<string?> MdnsNameAsync(IPAddress address, CancellationToken cancellationToken)
    {
        var octets = address.GetAddressBytes();
        var name = $"{octets[3]}.{octets[2]}.{octets[1]}.{octets[0]}.in-addr.arpa";
        var reply = await QueryAsync(address, MdnsPort, DnsWire.BuildQuery(name, (ushort)RandomNumberGenerator.GetInt32(65536), DnsWire.TypePtr), cancellationToken);
        return reply is null ? null : HostNames.Clean(DnsWire.ReadFirstPtr(reply));
    }

    private async Task<string?> NetBiosNameAsync(IPAddress address, CancellationToken cancellationToken)
    {
        var reply = await QueryAsync(address, NetBiosPort, NetBios.BuildNodeStatusQuery((ushort)RandomNumberGenerator.GetInt32(65536)), cancellationToken);
        return reply is null ? null : HostNames.Clean(NetBios.ParseComputerName(reply));
    }

    private async Task<string?> ReverseDnsAsync(IPAddress address, CancellationToken cancellationToken)
    {
        try
        {
            var entry = await Dns.GetHostEntryAsync(address).WaitAsync(EnrichTimeout, cancellationToken);
            return entry.HostName == address.ToString() ? null : HostNames.Clean(entry.HostName);
        }
        catch (Exception exception) when (exception is SocketException or TimeoutException or ArgumentException)
        {
            return null;
        }
    }

    private async Task<byte[]?> QueryAsync(IPAddress address, int port, byte[] query, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(EnrichTimeout);
        try
        {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            await socket.ConnectAsync(new IPEndPoint(address, port), timeout.Token);
            await socket.SendAsync(query, timeout.Token);
            var buffer = new byte[1500];
            var received = await socket.ReceiveAsync(buffer, timeout.Token);
            return buffer[..received];
        }
        catch (Exception exception) when (exception is SocketException or OperationCanceledException or ObjectDisposedException)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return null;
        }
    }

    private async Task<Dictionary<string, string>> DiscoverUpnpAsync(
        IReadOnlyList<CidrRange> ranges,
        IReadOnlyList<Candidate> hosts,
        CancellationToken cancellationToken)
    {
        var responses = new Dictionary<string, SsdpResponse>(StringComparer.Ordinal);
        foreach (var own in hosts.Where(host => host.OwnName is not null))
        {
            try
            {
                await ListenForUpnpAsync(own.Ip, ranges, responses, cancellationToken);
            }
            catch (Exception exception) when (exception is SocketException or ObjectDisposedException)
            {
                logger.LogDebug("UPnP discovery on {Address} failed: {Error}", own.Ip, exception.Message);
            }
        }

        var descriptions = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (ip, response) in responses.Take(MaxDescriptionLookups))
        {
            var name = await FriendlyNameAsync(ip, response, ranges, cancellationToken);
            if (name is not null)
            {
                descriptions[ip] = name;
            }
        }

        return descriptions;
    }

    private async Task ListenForUpnpAsync(
        IPAddress local,
        IReadOnlyList<CidrRange> ranges,
        Dictionary<string, SsdpResponse> responses,
        CancellationToken cancellationToken)
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        socket.Bind(new IPEndPoint(local, 0));
        socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastInterface, local.GetAddressBytes());
        socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastTimeToLive, 2);
        await socket.SendToAsync(Ssdp.BuildSearch(), Ssdp.Group, cancellationToken);

        using var window = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        window.CancelAfter(TimeSpan.FromSeconds(3));
        var buffer = new byte[4096];
        var any = new IPEndPoint(IPAddress.Any, 0);
        try
        {
            while (true)
            {
                var result = await socket.ReceiveFromAsync(buffer, SocketFlags.None, any, window.Token);
                if (result.RemoteEndPoint is IPEndPoint remote
                    && ranges.Any(range => range.Contains(remote.Address))
                    && Ssdp.ParseResponse(buffer.AsSpan(0, result.ReceivedBytes)) is { } response)
                {
                    responses.TryAdd(remote.Address.ToString(), response);
                }
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
        }
    }

    private static async Task<string?> FriendlyNameAsync(
        string ip, SsdpResponse response, IReadOnlyList<CidrRange> ranges, CancellationToken cancellationToken)
    {
        // A device on the LAN could point anywhere; only the address that answered is fetched.
        if (!Uri.TryCreate(response.Location, UriKind.Absolute, out var location)
            || location.Scheme != Uri.UriSchemeHttp
            || location.Host != ip
            || !IPAddress.TryParse(ip, out var address)
            || !ranges.Any(range => range.Contains(address)))
        {
            return HostNames.Clean(response.Server?.Split(' ').FirstOrDefault());
        }

        try
        {
            var xml = await Http.GetStringAsync(location, cancellationToken);
            return HostNames.Clean(Ssdp.ParseFriendlyName(xml)) ?? HostNames.Clean(response.Server?.Split(' ').FirstOrDefault());
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return null;
        }
    }

    private sealed record Candidate(IPAddress Ip, string Mac, string? OwnName = null);
}
