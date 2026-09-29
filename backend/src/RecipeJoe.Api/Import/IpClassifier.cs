using System.Net;
using System.Net.Sockets;

namespace RecipeJoe.Api.Import;

/// <summary>SSRF policy: which addresses Import may connect to.</summary>
internal static class IpClassifier
{
    public static bool IsPublic(IPAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);

        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        return address.AddressFamily switch
        {
            AddressFamily.InterNetwork => IsPublicV4(address.GetAddressBytes()),
            AddressFamily.InterNetworkV6 => IsPublicV6(address),
            _ => false,
        };
    }

    private static bool IsPublicV4(byte[] b) =>
        !(
            b[0] == 0 // "this" network, unspecified
            || b[0] == 10
            || (b[0] == 100 && b[1] is >= 64 and <= 127) // CGNAT
            || b[0] == 127
            || (b[0] == 169 && b[1] == 254) // link-local, incl. cloud metadata
            || (b[0] == 172 && b[1] is >= 16 and <= 31)
            || (b[0] == 192 && b[1] == 168)
            || b[0] >= 224 // multicast, reserved, broadcast
        );

    private static bool IsPublicV6(IPAddress address) =>
        !(
            address.Equals(IPAddress.IPv6None) // ::
            || address.Equals(IPAddress.IPv6Loopback)
            || address.IsIPv6LinkLocal
            || address.IsIPv6UniqueLocal
            || address.IsIPv6Multicast
            || address.IsIPv6SiteLocal
            || HasEmbeddedIpv4Prefix(address) // NAT64, 6to4: could smuggle a private IPv4
        );

    private static bool HasEmbeddedIpv4Prefix(IPAddress address)
    {
        Span<byte> b = stackalloc byte[16];
        address.TryWriteBytes(b, out _);
        var nat64 = b[..12].SequenceEqual(new byte[] { 0, 0x64, 0xff, 0x9b, 0, 0, 0, 0, 0, 0, 0, 0 });
        var sixToFour = b[0] == 0x20 && b[1] == 0x02;
        return nat64 || sixToFour;
    }
}
