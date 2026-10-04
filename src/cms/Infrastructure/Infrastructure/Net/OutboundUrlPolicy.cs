using System.Net;
using System.Net.Sockets;

namespace Infrastructure;

/// <summary>
/// Decides whether an operator-supplied URL is safe for the server to call out to.
/// <para>
/// Guards the features that fetch a URL the operator typed (the CDN probe) against
/// being aimed at the host's own network — the classic SSRF shape where
/// <c>http://169.254.169.254/…</c> or <c>http://127.0.0.1</c> turns a "test this
/// endpoint" setting into a way to reach internal services.
/// </para>
/// <para>
/// Deliberately bounded: it blocks a private/reserved <em>IP literal</em> and any
/// non-http(s) scheme, which stops the direct cases with no cost to legitimate public
/// URLs. It does not resolve hostnames — full DNS-rebinding defence is out of scope
/// for a setting only an authenticated operator can change.
/// </para>
/// </summary>
internal static class OutboundUrlPolicy
{
    public static bool IsPublicHttpUrl(string? url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri))
            return false;

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            return false;

        if (IPAddress.TryParse(uri.Host, out IPAddress? ip) && IsPrivateOrReserved(ip))
            return false;

        return true;
    }

    private static bool IsPrivateOrReserved(IPAddress ip)
    {
        if (IPAddress.IsLoopback(ip))
            return true;

        if (ip.AddressFamily == AddressFamily.InterNetwork)
        {
            byte[] b = ip.GetAddressBytes();
            if (b[0] is 10 or 0) return true;                          // 10.0.0.0/8, 0.0.0.0/8
            if (b[0] == 172 && b[1] >= 16 && b[1] <= 31) return true;  // 172.16.0.0/12
            if (b[0] == 192 && b[1] == 168) return true;              // 192.168.0.0/16
            if (b[0] == 169 && b[1] == 254) return true;              // 169.254.0.0/16 (link-local + cloud metadata)
            if (b[0] == 100 && b[1] >= 64 && b[1] <= 127) return true; // 100.64.0.0/10 (CGNAT)
            if (b[0] >= 224) return true;                             // multicast / reserved
            return false;
        }

        if (ip.AddressFamily == AddressFamily.InterNetworkV6)
        {
            return ip.IsIPv6LinkLocal
                || ip.IsIPv6SiteLocal
                || ip.IsIPv6UniqueLocal
                || ip.Equals(IPAddress.IPv6Any);
        }

        return false;
    }
}
