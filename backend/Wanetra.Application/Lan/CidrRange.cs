using System.Net;
using System.Net.Sockets;

namespace Wanetra.Application.Lan;

// An IPv4 network. Scanning is limited to private ranges and to at most 1,022 hosts.
public readonly record struct CidrRange(uint Network, int PrefixLength)
{
    public const int MinPrefixLength = 22;

    private static readonly CidrRange[] PrivateRanges =
    [
        new(0x0A000000, 8),   // 10.0.0.0/8
        new(0xAC100000, 12),  // 172.16.0.0/12
        new(0xC0A80000, 16),  // 192.168.0.0/16
        new(0xA9FE0000, 16),  // 169.254.0.0/16
    ];

    public uint Mask => PrefixLength == 0 ? 0 : uint.MaxValue << (32 - PrefixLength);

    public uint Broadcast => Network | ~Mask;

    public long HostCount => PrefixLength >= 31 ? 1L << (32 - PrefixLength) : (1L << (32 - PrefixLength)) - 2;

    public bool IsPrivate
    {
        get
        {
            var self = this;
            return PrivateRanges.Any(range => self.PrefixLength >= range.PrefixLength && (self.Network & range.Mask) == range.Network);
        }
    }

    public static CidrRange Of(IPAddress address, int prefixLength)
    {
        var mask = prefixLength == 0 ? 0 : uint.MaxValue << (32 - prefixLength);
        return new CidrRange(ToUInt(address) & mask, prefixLength);
    }

    public static bool TryParse(string? text, out CidrRange range)
    {
        range = default;
        var parts = text?.Trim().Split('/');
        if (parts is not { Length: 2 }
            || !IPAddress.TryParse(parts[0], out var address)
            || address.AddressFamily != AddressFamily.InterNetwork
            || !int.TryParse(parts[1], out var prefix)
            || prefix is < 0 or > 32)
        {
            return false;
        }

        range = Of(address, prefix);
        return true;
    }

    // Null when the range may be scanned, otherwise the reason it may not.
    public string? Problem() =>
        !IsPrivate ? "Only private address ranges (10.0.0.0/8, 172.16.0.0/12, 192.168.0.0/16, 169.254.0.0/16) can be scanned."
        : PrefixLength < MinPrefixLength ? $"The range is too large; use /{MinPrefixLength} or smaller."
        : null;

    public bool Contains(IPAddress address) =>
        address.AddressFamily == AddressFamily.InterNetwork && (ToUInt(address) & Mask) == Network;

    public IEnumerable<IPAddress> Hosts()
    {
        if (PrefixLength >= 31)
        {
            for (var value = (long)Network; value <= Broadcast; value++)
            {
                yield return ToAddress((uint)value);
            }

            yield break;
        }

        for (var value = (long)Network + 1; value < Broadcast; value++)
        {
            yield return ToAddress((uint)value);
        }
    }

    public override string ToString() => $"{ToAddress(Network)}/{PrefixLength}";

    private static uint ToUInt(IPAddress address)
    {
        var bytes = address.GetAddressBytes();
        return ((uint)bytes[0] << 24) | ((uint)bytes[1] << 16) | ((uint)bytes[2] << 8) | bytes[3];
    }

    private static IPAddress ToAddress(uint value) =>
        new([(byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value]);
}
