using System.Net;
using Wanetra.Application.Lan;
using Wanetra.Infrastructure.Lan;
using Wanetra.Infrastructure.Probes;

namespace Wanetra.Api.Tests.Lan;

public class LanHelperTests
{
    [Fact]
    public void A_network_is_parsed_and_host_bits_are_cleared()
    {
        Assert.True(CidrRange.TryParse("192.168.1.77/24", out var range));

        Assert.Equal("192.168.1.0/24", range.ToString());
        Assert.Equal(254, range.HostCount);
        Assert.Equal(IPAddress.Parse("192.168.1.1"), range.Hosts().First());
        Assert.Equal(IPAddress.Parse("192.168.1.254"), range.Hosts().Last());
        Assert.Equal(254, range.Hosts().Count());
        Assert.True(range.Contains(IPAddress.Parse("192.168.1.200")));
        Assert.False(range.Contains(IPAddress.Parse("192.168.2.1")));
        Assert.False(range.Contains(IPAddress.IPv6Loopback));
    }

    [Theory]
    [InlineData("")]
    [InlineData("192.168.1.0")]
    [InlineData("192.168.1.0/33")]
    [InlineData("192.168.1.0/-1")]
    [InlineData("nonsense/24")]
    [InlineData("::1/64")]
    public void Malformed_networks_are_rejected(string text)
    {
        Assert.False(CidrRange.TryParse(text, out _));
    }

    [Theory]
    [InlineData("10.0.0.0/22", true)]
    [InlineData("172.16.5.0/24", true)]
    [InlineData("172.31.255.0/24", true)]
    [InlineData("192.168.0.0/16", false)] // too large
    [InlineData("10.0.0.0/8", false)]
    [InlineData("172.32.0.0/24", false)] // just outside 172.16/12
    [InlineData("8.8.8.0/24", false)]
    [InlineData("100.64.0.0/24", false)]
    [InlineData("169.254.1.0/24", true)]
    [InlineData("192.169.0.0/24", false)]
    public void Only_small_private_networks_may_be_scanned(string text, bool allowed)
    {
        Assert.True(CidrRange.TryParse(text, out var range));

        Assert.Equal(allowed, range.Problem() is null);
    }

    [Fact]
    public void Tiny_networks_enumerate_every_address()
    {
        Assert.True(CidrRange.TryParse("192.168.1.8/31", out var pair));
        Assert.True(CidrRange.TryParse("192.168.1.8/32", out var single));
        Assert.True(CidrRange.TryParse("192.168.1.8/30", out var four));

        Assert.Equal(["192.168.1.8", "192.168.1.9"], pair.Hosts().Select(ip => ip.ToString()));
        Assert.Equal(["192.168.1.8"], single.Hosts().Select(ip => ip.ToString()));
        Assert.Equal(["192.168.1.9", "192.168.1.10"], four.Hosts().Select(ip => ip.ToString()));
    }

    [Theory]
    [InlineData("AA:BB:CC:DD:EE:FF", "aa:bb:cc:dd:ee:ff")]
    [InlineData("aa-bb-cc-dd-ee-f0", "aa:bb:cc:dd:ee:f0")]
    [InlineData("aabb.ccdd.eef0", "aa:bb:cc:dd:ee:f0")]
    [InlineData("aabbccddeef0", "aa:bb:cc:dd:ee:f0")]
    [InlineData("  AA:BB:CC:DD:EE:F0 ", "aa:bb:cc:dd:ee:f0")]
    public void Mac_addresses_are_normalized(string input, string expected)
    {
        Assert.Equal(expected, MacAddress.Normalize(input));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("00:00:00:00:00:00")]
    [InlineData("ff:ff:ff:ff:ff:ff")]
    [InlineData("01:00:5e:00:00:fb")] // multicast
    [InlineData("aa:bb:cc:dd:ee")]
    [InlineData("aa:bb:cc:dd:ee:gg")]
    [InlineData("aa:bb:cc:dd:ee:ff:00")]
    [InlineData("a:b:c:d:e:f:1")]
    public void Addresses_that_do_not_identify_one_device_are_rejected(string? input)
    {
        Assert.Null(MacAddress.Normalize(input));
    }

    [Fact]
    public void Randomized_addresses_are_recognized_by_the_locally_administered_bit()
    {
        Assert.True(MacAddress.IsRandomized("da:a1:19:00:00:01"));
        Assert.True(MacAddress.IsRandomized("02:42:ac:11:00:02"));
        Assert.False(MacAddress.IsRandomized("b8:27:eb:00:00:01"));
        Assert.Equal("B827EB", MacAddress.Prefix("b8:27:eb:00:00:01"));
    }

    [Fact]
    public void The_neighbour_table_yields_only_completed_entries_with_real_addresses()
    {
        const string table = """
            IP address       HW type     Flags       HW address            Mask     Device
            192.168.1.1      0x1         0x2         AA:BB:CC:00:00:01     *        eth0
            192.168.1.20     0x1         0x0         00:00:00:00:00:00     *        eth0
            192.168.1.21     0x1         0x2         aa:bb:cc:00:00:02     *        eth0
            192.168.1.22     0x1         0x6         aa:bb:cc:00:00:03     *        eth0
            garbage line
            10.0.0.5         0x1         0x2         00:00:00:00:00:00     *        eth1
            """;

        var entries = ArpTable.Parse(table);

        Assert.Equal(["192.168.1.1", "192.168.1.21", "192.168.1.22"], entries.Select(entry => entry.Ip.ToString()));
        Assert.Equal("aa:bb:cc:00:00:01", entries[0].Mac);
        Assert.Equal("eth0", entries[0].Interface);
    }

    [Fact]
    public void Device_names_lose_their_search_domain_and_unprintable_characters()
    {
        Assert.Equal("ali-pc", HostNames.Clean("ali-pc.lan"));
        Assert.Equal("Living Room", HostNames.Clean("Living Room.local."));
        Assert.Equal("nas", HostNames.Clean("nas.home.arpa"));
        Assert.Equal("evilname", HostNames.Clean("evil\u001b[31mname".Replace("[31m", string.Empty)));
        Assert.Equal("a.example.com", HostNames.Clean("a.example.com"));
        Assert.Equal(64, HostNames.Clean(new string('x', 200))!.Length);
        Assert.Null(HostNames.Clean(".local"));
        Assert.Null(HostNames.Clean("  "));
        Assert.Null(HostNames.Clean(null));
    }

    [Fact]
    public void A_ptr_answer_is_read_even_when_the_name_is_compressed()
    {
        // Question 4.3.2.1.in-addr.arpa PTR, answer name is a pointer to the question, rdata is "iphone.local".
        byte[] message =
        [
            0, 1, 0x84, 0, 0, 1, 0, 1, 0, 0, 0, 0,
            1, (byte)'4', 1, (byte)'3', 1, (byte)'2', 1, (byte)'1', 7, .. "in-addr"u8.ToArray(), 4, .. "arpa"u8.ToArray(), 0, 0, 12, 0, 1,
            0xC0, 12, 0, 12, 0, 1, 0, 0, 0, 120, 0, 14,
            6, .. "iphone"u8.ToArray(), 5, .. "local"u8.ToArray(), 0,
        ];

        Assert.Equal("iphone.local", DnsWire.ReadFirstPtr(message));
    }

    [Fact]
    public void Truncated_or_looping_dns_messages_are_ignored()
    {
        Assert.Null(DnsWire.ReadFirstPtr([0, 1]));
        // A name that points at itself must not hang the reader.
        byte[] loop = [0, 1, 0x84, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0xC0, 12, 0, 12, 0, 1];
        Assert.Null(DnsWire.ReadFirstPtr(loop));
        // An error response carries no answer.
        Assert.Null(DnsWire.ReadFirstPtr([0, 1, 0x84, 3, 0, 0, 0, 1, 0, 0, 0, 0]));
    }

    [Fact]
    public void A_netbios_node_status_response_yields_the_computer_name()
    {
        var name = new List<byte>();
        foreach (var (text, suffix, flags) in new[] { ("WORKGROUP", (byte)0x00, (ushort)0x8400), ("ALI-PC", (byte)0x00, (ushort)0x0400), ("ALI-PC", (byte)0x20, (ushort)0x0400) })
        {
            name.AddRange(System.Text.Encoding.ASCII.GetBytes(text.PadRight(15)));
            name.Add(suffix);
            name.Add((byte)(flags >> 8));
            name.Add((byte)flags);
        }

        var rdata = new List<byte> { 3 };
        rdata.AddRange(name);
        rdata.AddRange(new byte[46]);
        var message = new List<byte> { 0x12, 0x34, 0x84, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0x20 };
        message.AddRange("CKAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA"u8.ToArray());
        message.AddRange([0, 0, 0x21, 0, 1, 0, 0, 0, 0, 0, (byte)rdata.Count]);
        message.AddRange(rdata);

        Assert.Equal("ALI-PC", NetBios.ParseComputerName(message.ToArray()));
        Assert.Null(NetBios.ParseComputerName([1, 2, 3]));
        Assert.Equal(50, NetBios.BuildNodeStatusQuery(1).Length);
    }

    [Fact]
    public void A_upnp_reply_gives_the_description_location_and_server()
    {
        var reply = "HTTP/1.1 200 OK\r\nCACHE-CONTROL: max-age=120\r\nLOCATION: http://192.168.1.1:5000/desc.xml\r\nSERVER: Linux/4.9 UPnP/1.0 MiniUPnPd/2.1\r\nST: upnp:rootdevice\r\n\r\n";

        var parsed = Ssdp.ParseResponse(System.Text.Encoding.ASCII.GetBytes(reply));

        Assert.Equal("http://192.168.1.1:5000/desc.xml", parsed!.Location);
        Assert.Equal("Linux/4.9 UPnP/1.0 MiniUPnPd/2.1", parsed.Server);
        Assert.Null(Ssdp.ParseResponse("NOTIFY * HTTP/1.1\r\n\r\n"u8));
        Assert.Null(Ssdp.ParseResponse([]));
    }

    [Fact]
    public void The_friendly_name_is_read_from_a_description_and_xml_attacks_are_refused()
    {
        const string description = """
            <?xml version="1.0"?>
            <root xmlns="urn:schemas-upnp-org:device-1-0"><device><friendlyName> Living room TV </friendlyName></device></root>
            """;
        const string withEntity = """
            <?xml version="1.0"?>
            <!DOCTYPE root [<!ENTITY xxe SYSTEM "file:///etc/passwd">]>
            <root><device><friendlyName>&xxe;</friendlyName></device></root>
            """;

        Assert.Equal("Living room TV", Ssdp.ParseFriendlyName(description));
        Assert.Null(Ssdp.ParseFriendlyName(withEntity));
        Assert.Null(Ssdp.ParseFriendlyName("<root><device/></root>"));
        Assert.Null(Ssdp.ParseFriendlyName("not xml"));
    }

    [Fact]
    public void The_vendor_table_knows_common_manufacturers()
    {
        var oui = new EmbeddedOuiDatabase();

        Assert.True(oui.Count > 30_000);
        Assert.Contains("Raspberry Pi", oui.Lookup("b8:27:eb:12:34:56"));
        Assert.Contains("Raspberry Pi", oui.Lookup("B8-27-EB-12-34-56"));
        Assert.Null(oui.Lookup("da:a1:19:00:00:01"));
        Assert.Null(oui.Lookup("not a mac"));
    }

    private static InterfaceAddress Iface(string name, string ip, int prefix, byte[]? mac = null, bool up = true) =>
        new(name, IPAddress.Parse(ip), prefix, up, mac ?? [0xb8, 0x27, 0xeb, 0, 0, 1]);

    [Fact]
    public void The_lan_is_the_private_network_of_a_real_interface()
    {
        var detection = LanNetworkDetector.Select(
        [
            Iface("lo", "127.0.0.1", 8),
            Iface("eth0", "192.168.1.5", 24),
            Iface("eth1", "10.0.0.7", 16),
            Iface("wlan0", "192.168.1.9", 24),
            Iface("eth2", "203.0.113.5", 24),
            Iface("ens3", "192.168.50.3", 32),
            Iface("eth3", "192.168.9.9", 24, up: false),
            Iface("wg0", "10.8.0.2", 24),
            Iface("docker0", "172.17.0.1", 16),
            Iface("tailscale0", "100.64.0.2", 10),
        ]);

        Assert.Equal(["192.168.1.0/24", "10.0.0.0/24"], detection.Networks.Select(network => network.Range.ToString()));
        Assert.Equal("eth0", detection.Networks[0].InterfaceName);
        Assert.False(detection.OnlyContainerNetworks);
    }

    [Fact]
    public void A_slash_22_is_kept_and_larger_networks_shrink_to_the_hosts_slash_24()
    {
        var detection = LanNetworkDetector.Select([Iface("eth0", "10.1.2.3", 22), Iface("eth1", "10.9.9.9", 8)]);

        Assert.Equal(["10.1.0.0/22", "10.9.9.0/24"], detection.Networks.Select(network => network.Range.ToString()));
    }

    [Fact]
    public void A_container_on_the_docker_bridge_sees_no_lan_and_says_so()
    {
        var detection = LanNetworkDetector.Select([Iface("eth0", "172.17.0.2", 16, mac: [0x02, 0x42, 0xac, 0x11, 0, 2])]);

        Assert.Empty(detection.Networks);
        Assert.True(detection.OnlyContainerNetworks);
    }

    [Fact]
    public void No_usable_interface_is_not_blamed_on_docker()
    {
        var detection = LanNetworkDetector.Select([Iface("lo", "127.0.0.1", 8)]);

        Assert.Empty(detection.Networks);
        Assert.False(detection.OnlyContainerNetworks);
    }
}
