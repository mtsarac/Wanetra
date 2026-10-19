using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Wanetra.Application.Probes;
using Wanetra.Domain;

namespace Wanetra.Infrastructure.Probes;

public sealed class NetworkProbeExecutor : IProbeExecutor, IDisposable
{
    // Ports tried when ICMP is not usable. A host that answers with a reset is up just the same.
    private static readonly int[] ReachabilityPorts = [443, 80];

    private readonly HttpClient httpClient;
    private readonly ILogger<NetworkProbeExecutor> logger;
    private volatile bool icmpUnavailable;

    public NetworkProbeExecutor(ILogger<NetworkProbeExecutor> logger)
    {
        this.logger = logger;
        httpClient = new HttpClient(new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            UseProxy = false,
            UseCookies = false,
        })
        {
            Timeout = Timeout.InfiniteTimeSpan,
        };
    }

    public bool IcmpUnavailable => icmpUnavailable;

    internal int ResolverPort { get; init; } = 53;

    public async Task<ProbeOutcome> ExecuteAsync(ProbeRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return request.Kind switch
            {
                ProbeKind.Icmp => await PingAsync(request, cancellationToken),
                ProbeKind.Tcp => await ConnectAsync(request.Host, request.Port ?? 0, request.Timeout, refusedIsUp: false, cancellationToken),
                ProbeKind.Dns => await ResolveAsync(request, cancellationToken),
                ProbeKind.Http => await FetchAsync(request, cancellationToken),
                _ => ProbeOutcome.Down("unsupported"),
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogDebug(exception, "Probe of {Host} failed", request.Host);
            return ProbeOutcome.Down("error");
        }
    }

    private async Task<ProbeOutcome> PingAsync(ProbeRequest request, CancellationToken cancellationToken)
    {
        if (!icmpUnavailable)
        {
            try
            {
                using var ping = new Ping();
                var reply = await ping.SendPingAsync(request.Host, request.Timeout, null, null, cancellationToken);
                return reply.Status switch
                {
                    IPStatus.Success => ProbeOutcome.Up(Math.Max(reply.RoundtripTime, 0)),
                    IPStatus.TimedOut => ProbeOutcome.Down("timeout"),
                    var status => ProbeOutcome.Down(status.ToString().ToLowerInvariant()),
                };
            }
            catch (Exception exception) when (exception is not OperationCanceledException && IsIcmpNotPermitted(exception))
            {
                icmpUnavailable = true;
                logger.LogWarning(
                    "ICMP is not available to this process; falling back to TCP reachability checks. "
                    + "Allow it with net.ipv4.ping_group_range or by running with CAP_NET_RAW");
            }
            catch (PingException exception) when (exception.InnerException is SocketException { SocketErrorCode: SocketError.HostNotFound or SocketError.TryAgain or SocketError.NoData })
            {
                return ProbeOutcome.Down("dns");
            }
            catch (PingException)
            {
                return ProbeOutcome.Down("error");
            }
        }

        return await ReachAsync(request, cancellationToken);
    }

    private static bool IsIcmpNotPermitted(Exception exception) =>
        exception is PlatformNotSupportedException or UnauthorizedAccessException or System.ComponentModel.Win32Exception
        || exception.InnerException is { } inner && IsIcmpNotPermitted(inner)
        || exception is SocketException
        {
            SocketErrorCode: SocketError.AccessDenied or SocketError.ProtocolNotSupported
                or SocketError.AddressFamilyNotSupported or SocketError.OperationNotSupported
        };

    private async Task<ProbeOutcome> ReachAsync(ProbeRequest request, CancellationToken cancellationToken)
    {
        var attempts = ReachabilityPorts
            .Select(port => ConnectAsync(request.Host, port, request.Timeout, refusedIsUp: true, cancellationToken))
            .ToList();

        ProbeOutcome last = ProbeOutcome.Down("timeout");
        while (attempts.Count > 0)
        {
            var finished = await Task.WhenAny(attempts);
            attempts.Remove(finished);
            last = await finished;
            if (last.Success)
            {
                return last;
            }
        }

        return last;
    }

    private static async Task<ProbeOutcome> ConnectAsync(
        string host, int port, TimeSpan timeout, bool refusedIsUp, CancellationToken cancellationToken)
    {
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);
        var stopwatch = Stopwatch.StartNew();
        using var client = new TcpClient();
        try
        {
            await client.ConnectAsync(host, port, timeoutSource.Token);
            return ProbeOutcome.Up(stopwatch.Elapsed.TotalMilliseconds);
        }
        catch (SocketException exception) when (exception.SocketErrorCode == SocketError.ConnectionRefused)
        {
            return refusedIsUp ? ProbeOutcome.Up(stopwatch.Elapsed.TotalMilliseconds) : ProbeOutcome.Down("refused");
        }
        catch (SocketException exception) when (exception.SocketErrorCode is SocketError.HostNotFound or SocketError.TryAgain or SocketError.NoData)
        {
            return ProbeOutcome.Down("dns");
        }
        catch (SocketException exception) when (exception.SocketErrorCode is SocketError.NetworkUnreachable or SocketError.HostUnreachable)
        {
            return ProbeOutcome.Down("unreachable");
        }
        catch (SocketException)
        {
            return ProbeOutcome.Down("error");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return ProbeOutcome.Down("timeout");
        }
    }

    private async Task<ProbeOutcome> ResolveAsync(ProbeRequest request, CancellationToken cancellationToken)
    {
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(request.Timeout);
        var stopwatch = Stopwatch.StartNew();
        try
        {
            if (request.Resolver is null)
            {
                var addresses = await Dns.GetHostAddressesAsync(request.Host, timeoutSource.Token);
                return addresses.Length > 0 ? ProbeOutcome.Up(stopwatch.Elapsed.TotalMilliseconds) : ProbeOutcome.Down("noanswer");
            }

            return await QueryResolverAsync(request.Host, IPAddress.Parse(request.Resolver), ResolverPort, stopwatch, timeoutSource.Token);
        }
        catch (SocketException)
        {
            return ProbeOutcome.Down("dns");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return ProbeOutcome.Down("timeout");
        }
    }

    private static async Task<ProbeOutcome> QueryResolverAsync(
        string name, IPAddress resolver, int port, Stopwatch stopwatch, CancellationToken cancellationToken)
    {
        var id = (ushort)RandomNumberGenerator.GetInt32(ushort.MaxValue + 1);
        using var socket = new Socket(resolver.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
        await socket.ConnectAsync(new IPEndPoint(resolver, port), cancellationToken);
        await socket.SendAsync(DnsWire.BuildQuery(name, id), cancellationToken);

        var buffer = new byte[1232];
        while (true)
        {
            var received = await socket.ReceiveAsync(buffer, cancellationToken);
            if (DnsWire.ParseReply(buffer.AsSpan(0, received)) is not { } reply || reply.Id != id)
            {
                continue;
            }

            return reply.ResponseCode switch
            {
                0 when reply.AnswerCount > 0 => ProbeOutcome.Up(stopwatch.Elapsed.TotalMilliseconds),
                0 => ProbeOutcome.Down("noanswer"),
                3 => ProbeOutcome.Down("nxdomain"),
                2 => ProbeOutcome.Down("servfail"),
                var code => ProbeOutcome.Down($"rcode {code}"),
            };
        }
    }

    private async Task<ProbeOutcome> FetchAsync(ProbeRequest request, CancellationToken cancellationToken)
    {
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(request.Timeout);
        var stopwatch = Stopwatch.StartNew();
        try
        {
            using var message = new HttpRequestMessage(HttpMethod.Get, request.Host);
            using var response = await httpClient.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, timeoutSource.Token);
            var elapsed = stopwatch.Elapsed.TotalMilliseconds;
            return (int)response.StatusCode < 400
                ? ProbeOutcome.Up(elapsed)
                : ProbeOutcome.Down($"http {(int)response.StatusCode}");
        }
        catch (HttpRequestException exception) when (exception.InnerException is SocketException { SocketErrorCode: SocketError.HostNotFound or SocketError.TryAgain or SocketError.NoData })
        {
            return ProbeOutcome.Down("dns");
        }
        catch (HttpRequestException)
        {
            return ProbeOutcome.Down("error");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return ProbeOutcome.Down("timeout");
        }
    }

    public void Dispose() => httpClient.Dispose();
}
