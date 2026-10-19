using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging.Abstractions;
using Wanetra.Application.Lan;
using Wanetra.Infrastructure.Lan;

namespace Wanetra.Api.Tests.Lan;

public class NetworkLanScannerTests : IDisposable
{
    private readonly string arpPath = Path.Combine(Path.GetTempPath(), $"wanetra-arp-{Guid.NewGuid():N}");

    public void Dispose()
    {
        File.Delete(arpPath);
        GC.SuppressFinalize(this);
    }

    private NetworkLanScanner Scanner(int mdnsPort = 9, int netBiosPort = 9) => new(NullLogger<NetworkLanScanner>.Instance)
    {
        ArpPath = arpPath,
        Settle = TimeSpan.Zero,
        EnrichTimeout = TimeSpan.FromMilliseconds(300),
        MdnsPort = mdnsPort,
        NetBiosPort = netBiosPort,
        UseSsdp = false,
    };

    private static readonly CidrRange Loopback = CidrRange.Of(IPAddress.Parse("127.0.0.0"), 29);

    [Fact]
    public async Task Devices_in_the_neighbour_table_are_returned_with_their_addresses_in_order()
    {
        await File.WriteAllTextAsync(arpPath, """
            IP address       HW type     Flags       HW address            Mask     Device
            127.0.0.5        0x1         0x2         aa:bb:cc:00:00:05     *        lo
            127.0.0.2        0x1         0x2         aa:bb:cc:00:00:02     *        lo
            127.0.0.3        0x1         0x0         00:00:00:00:00:00     *        lo
            192.168.77.4     0x1         0x2         aa:bb:cc:00:00:09     *        eth0
            """);

        var result = await Scanner().ScanAsync([Loopback], CancellationToken.None);

        Assert.Null(result.Error);
        var hosts = result.Hosts.Where(host => host.Mac.StartsWith("aa:bb:cc", StringComparison.Ordinal)).ToList();
        Assert.Equal(["127.0.0.2", "127.0.0.5"], hosts.Select(host => host.Ip));
        Assert.Equal(["aa:bb:cc:00:00:02", "aa:bb:cc:00:00:05"], hosts.Select(host => host.Mac));
    }

    [Fact]
    public async Task A_device_that_answers_mdns_and_netbios_is_named_with_mdns_taking_priority()
    {
        await File.WriteAllTextAsync(arpPath, """
            IP address       HW type     Flags       HW address            Mask     Device
            127.0.0.2        0x1         0x2         aa:bb:cc:00:00:02     *        lo
            127.0.0.4        0x1         0x2         aa:bb:cc:00:00:04     *        lo
            """);
        using var mdns = new UdpResponder(query => MdnsReply(query, "kitchen-display.local"));
        using var netbios = new UdpResponder(query => NetBiosReply(query, "OFFICE-PC"));

        var result = await Scanner(mdns.Port, netbios.Port).ScanAsync([Loopback], CancellationToken.None);

        var named = result.Hosts.First(host => host.Mac == "aa:bb:cc:00:00:02");
        Assert.Equal("kitchen-display", named.Hostname);
    }

    [Fact]
    public async Task Netbios_is_used_when_the_device_does_not_speak_mdns()
    {
        await File.WriteAllTextAsync(arpPath, """
            IP address       HW type     Flags       HW address            Mask     Device
            127.0.0.2        0x1         0x2         aa:bb:cc:00:00:02     *        lo
            """);
        using var netbios = new UdpResponder(query => NetBiosReply(query, "OFFICE-PC"));

        var result = await Scanner(netBiosPort: netbios.Port).ScanAsync([Loopback], CancellationToken.None);

        Assert.Equal("OFFICE-PC", result.Hosts.First(host => host.Mac == "aa:bb:cc:00:00:02").Hostname);
    }

    [Fact]
    public async Task Addresses_outside_the_scanned_range_are_ignored()
    {
        await File.WriteAllTextAsync(arpPath, """
            IP address       HW type     Flags       HW address            Mask     Device
            192.168.77.4     0x1         0x2         aa:bb:cc:00:00:09     *        eth0
            """);

        var result = await Scanner().ScanAsync([Loopback], CancellationToken.None);

        Assert.DoesNotContain(result.Hosts, host => host.Mac == "aa:bb:cc:00:00:09");
    }

    [Fact]
    public async Task Without_a_neighbour_table_the_scan_explains_why()
    {
        var result = await Scanner().ScanAsync([Loopback], CancellationToken.None);

        Assert.Empty(result.Hosts);
        Assert.Contains("only works on Linux", result.Error);
    }

    [Fact]
    public async Task Cancelling_stops_the_scan()
    {
        await File.WriteAllTextAsync(arpPath, "IP address HW type Flags HW address Mask Device\n");
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var scanner = new NetworkLanScanner(NullLogger<NetworkLanScanner>.Instance) { ArpPath = arpPath, Settle = TimeSpan.FromSeconds(30), UseSsdp = false };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => scanner.ScanAsync([Loopback], cts.Token));
    }

    private static byte[] MdnsReply(byte[] query, string name)
    {
        var reply = new List<byte>(query.Take(12)) { };
        reply[2] = 0x84;
        reply[7] = 1;
        reply.AddRange(query.Skip(12));
        reply.AddRange([0xC0, 12, 0, 12, 0, 1, 0, 0, 0, 120]);
        var labels = name.Split('.').SelectMany(label => new byte[] { (byte)label.Length }.Concat(System.Text.Encoding.ASCII.GetBytes(label))).Append((byte)0).ToArray();
        reply.AddRange([(byte)(labels.Length >> 8), (byte)labels.Length]);
        reply.AddRange(labels);
        return [.. reply];
    }

    private static byte[] NetBiosReply(byte[] query, string name)
    {
        var rdata = new List<byte> { 1 };
        rdata.AddRange(System.Text.Encoding.ASCII.GetBytes(name.PadRight(15)));
        rdata.AddRange([0x00, 0x04, 0x00]);
        rdata.AddRange(new byte[46]);
        var reply = new List<byte> { query[0], query[1], 0x84, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0x20 };
        reply.AddRange("CKAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA"u8.ToArray());
        reply.AddRange([0, 0, 0x21, 0, 1, 0, 0, 0, 0, 0, (byte)rdata.Count]);
        reply.AddRange(rdata);
        return [.. reply];
    }

    private sealed class UdpResponder : IDisposable
    {
        private readonly UdpClient udp = new(new IPEndPoint(IPAddress.Parse("127.0.0.2"), 0));

        public UdpResponder(Func<byte[], byte[]> reply)
        {
            Port = ((IPEndPoint)udp.Client.LocalEndPoint!).Port;
            _ = Task.Run(async () =>
            {
                try
                {
                    while (true)
                    {
                        var request = await udp.ReceiveAsync();
                        await udp.SendAsync(reply(request.Buffer), request.RemoteEndPoint);
                    }
                }
                catch (Exception exception) when (exception is ObjectDisposedException or SocketException)
                {
                }
            });
        }

        public int Port { get; }

        public void Dispose() => udp.Dispose();
    }
}
