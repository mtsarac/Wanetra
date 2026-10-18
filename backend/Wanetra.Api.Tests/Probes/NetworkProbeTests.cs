using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Wanetra.Application.Probes;
using Wanetra.Domain;
using Wanetra.Infrastructure.Probes;

namespace Wanetra.Api.Tests.Probes;

public class NetworkProbeTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(2);

    private static NetworkProbeExecutor NewExecutor(int resolverPort = 53) =>
        new(NullLogger<NetworkProbeExecutor>.Instance) { ResolverPort = resolverPort };

    private static ProbeRequest Request(ProbeKind kind, string host, int? port = null, string? resolver = null) =>
        new(kind, host, port, resolver, Timeout);

    private static int FreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    [Fact]
    public async Task Tcp_probe_is_up_when_the_port_accepts()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        using var executor = NewExecutor();

        var outcome = await executor.ExecuteAsync(Request(ProbeKind.Tcp, "127.0.0.1", port), CancellationToken.None);

        Assert.True(outcome.Success);
        Assert.True(outcome.RttMs >= 0);
    }

    [Fact]
    public async Task Tcp_probe_is_down_when_the_port_refuses()
    {
        using var executor = NewExecutor();

        var outcome = await executor.ExecuteAsync(Request(ProbeKind.Tcp, "127.0.0.1", FreePort()), CancellationToken.None);

        Assert.False(outcome.Success);
        Assert.Equal("refused", outcome.Error);
    }

    [Fact]
    public async Task Http_probe_is_up_for_success_and_redirect_statuses_and_down_for_errors()
    {
        using var executor = NewExecutor();
        foreach (var (status, expectedUp) in new[] { (204, true), (302, true), (503, false), (404, false) })
        {
            using var server = new TinyHttpServer(status);

            var outcome = await executor.ExecuteAsync(Request(ProbeKind.Http, server.Url), CancellationToken.None);

            Assert.Equal(expectedUp, outcome.Success);
            if (!expectedUp)
            {
                Assert.Equal($"http {status}", outcome.Error);
            }
        }
    }

    [Fact]
    public async Task Http_probe_does_not_follow_redirects()
    {
        using var server = new TinyHttpServer(302);
        using var executor = NewExecutor();

        var outcome = await executor.ExecuteAsync(Request(ProbeKind.Http, server.Url), CancellationToken.None);

        Assert.True(outcome.Success);
        Assert.Equal(1, server.Requests);
    }

    [Fact]
    public async Task Http_probe_is_down_when_nothing_listens()
    {
        using var executor = NewExecutor();

        var outcome = await executor.ExecuteAsync(Request(ProbeKind.Http, $"http://127.0.0.1:{FreePort()}/"), CancellationToken.None);

        Assert.False(outcome.Success);
    }

    [Fact]
    public async Task Dns_probe_with_the_system_resolver_resolves_localhost()
    {
        using var executor = NewExecutor();

        var outcome = await executor.ExecuteAsync(Request(ProbeKind.Dns, "localhost"), CancellationToken.None);

        Assert.True(outcome.Success);
    }

    [Fact]
    public async Task Dns_probe_asks_the_configured_resolver_and_reads_the_verdict()
    {
        foreach (var (rcode, answers, expectedUp, expectedError) in new[]
        {
            (0, 1, true, (string?)null),
            (0, 0, false, "noanswer"),
            (3, 0, false, "nxdomain"),
            (2, 0, false, "servfail"),
        })
        {
            using var resolver = new StubResolver(rcode, answers);
            using var executor = NewExecutor(resolver.Port);

            var outcome = await executor.ExecuteAsync(Request(ProbeKind.Dns, "example.com", resolver: "127.0.0.1"), CancellationToken.None);

            Assert.Equal(expectedUp, outcome.Success);
            Assert.Equal(expectedError, outcome.Error);
            Assert.Equal("example.com", resolver.LastName);
        }
    }

    [Fact]
    public async Task Dns_probe_times_out_when_the_resolver_stays_silent()
    {
        using var silent = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var port = ((IPEndPoint)silent.Client.LocalEndPoint!).Port;
        using var executor = NewExecutor(port);

        var outcome = await executor.ExecuteAsync(
            new ProbeRequest(ProbeKind.Dns, "example.com", null, "127.0.0.1", TimeSpan.FromMilliseconds(300)),
            CancellationToken.None);

        Assert.False(outcome.Success);
        Assert.Equal("timeout", outcome.Error);
    }

    [Fact]
    public async Task Cancellation_propagates_instead_of_counting_as_a_failed_probe()
    {
        using var executor = NewExecutor();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            executor.ExecuteAsync(Request(ProbeKind.Tcp, "127.0.0.1", FreePort()), cts.Token));
    }

    [Fact]
    public void Dns_queries_are_wire_correct()
    {
        var query = DnsWire.BuildQuery("example.com", 0xABCD);

        Assert.Equal(
            [0xAB, 0xCD, 0x01, 0x00, 0x00, 0x01, 0, 0, 0, 0, 0, 0,
             7, .. "example"u8.ToArray(), 3, .. "com"u8.ToArray(), 0, 0x00, 0x01, 0x00, 0x01],
            query);
        Assert.Equal(query, DnsWire.BuildQuery("example.com.", 0xABCD));
    }

    [Theory]
    [InlineData("")]
    [InlineData("a..b")]
    public void Invalid_dns_names_are_refused(string name)
    {
        Assert.Throws<ArgumentException>(() => DnsWire.BuildQuery(name, 1));
    }

    [Fact]
    public void Dns_replies_are_parsed_and_junk_is_ignored()
    {
        byte[] reply = [0x12, 0x34, 0x81, 0x83, 0, 1, 0, 0, 0, 0, 0, 0];

        var parsed = DnsWire.ParseReply(reply);

        Assert.Equal(new DnsReply(0x1234, 3, 0), parsed);
        Assert.Null(DnsWire.ParseReply([1, 2, 3]));
        Assert.Null(DnsWire.ParseReply([0x12, 0x34, 0x01, 0x00, 0, 1, 0, 0, 0, 0, 0, 0]));
    }

    [Fact]
    public void The_default_gateway_is_the_lowest_metric_default_route()
    {
        const string table = """
            Iface	Destination	Gateway 	Flags	RefCnt	Use	Metric	Mask		MTU	Window	IRTT
            wlan0	00000000	0100000A	0003	0	0	600	00000000	0	0	0
            eth0	00000000	0100A8C0	0003	0	0	100	00000000	0	0	0
            eth0	0000A8C0	00000000	0001	0	0	100	00FFFFFF	0	0	0
            """;

        Assert.Equal("192.168.0.1", LinuxGatewayDetector.Parse(table));
    }

    [Theory]
    [InlineData("")]
    [InlineData("Iface\tDestination\tGateway\tFlags\tRefCnt\tUse\tMetric\tMask\n")]
    [InlineData("Iface\tDestination\tGateway\tFlags\tRefCnt\tUse\tMetric\tMask\neth0\t0000A8C0\t00000000\t0001\t0\t0\t100\t00FFFFFF\n")]
    [InlineData("Iface\tDestination\tGateway\tFlags\tRefCnt\tUse\tMetric\tMask\neth0\t00000000\t00000000\t0001\t0\t0\t100\t00000000\n")]
    [InlineData("Iface\tDestination\tGateway\tFlags\tRefCnt\tUse\tMetric\tMask\neth0\t00000000\t0100A8C0\t0001\t0\t0\t100\t00000000\n")]
    public void Tables_without_a_usable_default_route_have_no_gateway(string table)
    {
        Assert.Null(LinuxGatewayDetector.Parse(table));
    }

    [Fact]
    public void A_missing_route_table_means_no_gateway()
    {
        Assert.Null(new LinuxGatewayDetector(Path.Combine(Path.GetTempPath(), "no-such-route-table")).DetectGateway());
    }

    private sealed class TinyHttpServer : IDisposable
    {
        private readonly TcpListener listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource stop = new();
        private int requests;

        public TinyHttpServer(int status)
        {
            listener.Start();
            Url = $"http://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}/";
            _ = Task.Run(async () =>
            {
                try
                {
                    while (!stop.IsCancellationRequested)
                    {
                        using var client = await listener.AcceptTcpClientAsync(stop.Token);
                        var stream = client.GetStream();
                        var buffer = new byte[4096];
                        _ = await stream.ReadAsync(buffer, stop.Token);
                        Interlocked.Increment(ref requests);
                        var location = status == 302 ? "Location: /elsewhere\r\n" : string.Empty;
                        await stream.WriteAsync(Encoding.ASCII.GetBytes($"HTTP/1.1 {status} X\r\n{location}Content-Length: 0\r\nConnection: close\r\n\r\n"), stop.Token);
                    }
                }
                catch (Exception exception) when (exception is OperationCanceledException or ObjectDisposedException or SocketException or IOException)
                {
                }
            });
        }

        public string Url { get; }

        public int Requests => Volatile.Read(ref requests);

        public void Dispose()
        {
            stop.Cancel();
            listener.Stop();
            stop.Dispose();
        }
    }

    private sealed class StubResolver : IDisposable
    {
        private readonly UdpClient udp = new(new IPEndPoint(IPAddress.Loopback, 0));

        public StubResolver(int responseCode, int answerCount)
        {
            Port = ((IPEndPoint)udp.Client.LocalEndPoint!).Port;
            _ = Task.Run(async () =>
            {
                try
                {
                    var received = await udp.ReceiveAsync();
                    LastName = ReadName(received.Buffer);
                    var reply = received.Buffer.Take(12).ToList();
                    reply[2] = 0x81;
                    reply[3] = (byte)(0x80 | responseCode);
                    reply[6] = 0;
                    reply[7] = (byte)answerCount;
                    // An unrelated datagram first: a client must keep waiting for the matching id.
                    var stray = reply.ToArray();
                    stray[0] ^= 0xFF;
                    await udp.SendAsync(stray, stray.Length, received.RemoteEndPoint);
                    await udp.SendAsync(reply.ToArray(), reply.Count, received.RemoteEndPoint);
                }
                catch (Exception exception) when (exception is ObjectDisposedException or SocketException)
                {
                }
            });
        }

        public int Port { get; }

        public string? LastName { get; private set; }

        private static string ReadName(byte[] query)
        {
            var labels = new List<string>();
            for (var i = 12; i < query.Length && query[i] != 0; i += query[i] + 1)
            {
                labels.Add(Encoding.ASCII.GetString(query, i + 1, query[i]));
            }

            return string.Join('.', labels);
        }

        public void Dispose() => udp.Dispose();
    }
}
